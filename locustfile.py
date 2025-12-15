import time
import json
import numpy as np
from locust import HttpUser, task, between, events

# Конфигурация теста
MATRIX_SIZE = 300  # Размер матрицы для теста (300x300)
POLL_INTERVAL = 0.5  # Интервал опроса статуса (сек)
MAX_RETRIES = 60  # Максимальное количество попыток опроса (30 сек)


class SolverUser(HttpUser):
    wait_time = between(1, 3)  # Пауза между задачами от одного юзера

    def generate_system(self, size):
        """Генерирует случайную СЛАУ Ax=b"""
        matrix = np.random.rand(size, size).tolist()
        free_terms = np.random.rand(size).tolist()
        return matrix, free_terms

    @task(3)
    def solve_distributed(self):
        """Сценарий: Решение распределенным методом (частый кейс)"""
        self.run_solve_scenario("distributed")

    @task(1)
    def solve_linear(self):
        """Сценарий: Решение линейным методом (редкий кейс)"""
        self.run_solve_scenario("linear")

    def run_solve_scenario(self, method):
        matrix, free_terms = self.generate_system(MATRIX_SIZE)

        payload = {
            "matrix": matrix,
            "freeTerms": free_terms
        }

        # 1. Отправка задачи
        with self.client.post(
                f"/api/Coordinator/solve?method={method}",
                json=payload,
                catch_response=True
        ) as response:
            if response.status_code != 200:
                response.failure(f"Submit failed: {response.text}")
                return

            try:
                task_id = response.json().get("taskId")
            except json.JSONDecodeError:
                response.failure("Invalid JSON response")
                return

        if not task_id:
            return

        # 2. Ожидание результата (Polling)
        start_time = time.time()
        for _ in range(MAX_RETRIES):
            with self.client.get(
                    f"/api/Coordinator/tasks/status?taskIds={task_id}",
                    name="/api/Coordinator/tasks/status",
                    catch_response=True
            ) as poll_response:

                if poll_response.status_code != 200:
                    poll_response.failure(f"Poll failed: {poll_response.text}")
                    break

                statuses = poll_response.json()
                if not statuses:
                    continue

                task_status = statuses[0]
                status_str = task_status.get("status")

                if status_str == "Completed":
                    total_time = (time.time() - start_time) * 1000
                    events.request.fire(
                        request_type="SCENARIO",
                        name=f"Full_Solve_{method}",
                        response_time=total_time,
                        response_length=len(json.dumps(task_status)),
                    )
                    return

                elif status_str == "Failed":
                    events.request.fire(
                        request_type="SCENARIO",
                        name=f"Full_Solve_{method}",
                        response_time=(time.time() - start_time) * 1000,
                        exception=Exception(f"Task failed on worker: {task_status.get('error')}")
                    )
                    return

            time.sleep(POLL_INTERVAL)