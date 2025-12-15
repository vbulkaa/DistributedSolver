import docker
import time
import csv
import datetime
import threading

# Настройки
OUTPUT_FILE = "resource_usage.csv"
CONTAINER_NAMES = ["coordinator", "worker1", "worker2", "worker3"]  # Добавьте workerN, если их больше
INTERVAL = 1  # Интервал замеров (сек)


def get_stats():
    client = docker.from_env()

    with open(OUTPUT_FILE, 'w', newline='') as csvfile:
        fieldnames = ['timestamp', 'container', 'cpu_percent', 'memory_usage_mb']
        writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
        writer.writeheader()

        print(f"Starting monitoring for: {CONTAINER_NAMES}")
        print("Press Ctrl+C to stop...")

        try:
            while True:
                timestamp = datetime.datetime.now().isoformat()

                for name in CONTAINER_NAMES:
                    try:
                        container = client.containers.get(name)  # Имя контейнера из docker-compose
                        stats = container.stats(stream=False)

                        # Расчет CPU (Docker API возвращает сырые данные)
                        cpu_delta = stats['cpu_stats']['cpu_usage']['total_usage'] - \
                                    stats['precpu_stats']['cpu_usage']['total_usage']
                        system_cpu_delta = stats['cpu_stats']['system_cpu_usage'] - \
                                           stats['precpu_stats']['system_cpu_usage']
                        number_cpus = stats['cpu_stats']['online_cpus']

                        if system_cpu_delta > 0.0:
                            cpu_percent = (cpu_delta / system_cpu_delta) * number_cpus * 100.0
                        else:
                            cpu_percent = 0.0

                        # Расчет RAM
                        memory_usage = stats['memory_stats']['usage'] / (1024 * 1024)  # MB

                        writer.writerow({
                            'timestamp': timestamp,
                            'container': name,
                            'cpu_percent': round(cpu_percent, 2),
                            'memory_usage_mb': round(memory_usage, 2)
                        })

                    except Exception as e:
                        # Контейнер может быть выключен или не найден
                        pass

                csvfile.flush()
                time.sleep(INTERVAL)
        except KeyboardInterrupt:
            print("\nMonitoring stopped.")


if __name__ == "__main__":
    get_stats()