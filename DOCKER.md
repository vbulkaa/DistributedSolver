# Distributed Solver + Docker

## Обзор

Проект полностью готов к развертыванию через Docker Compose. Все сервисы автоматически собираются, настраиваются и запускаются в изолированной сети.

## Быстрый старт

### 1. Поднять весь стенд

```bash
docker-compose up --build
```

Собираются образы координатора и всех compute-нод, создаётся сеть `distributed-solver-network`, пробрасываются порты `5000` (координатор) и `6001-6006` (воркеры).

### 2. Проверка работоспособности

```bash
# Проверка координатора
curl http://localhost:5000/api/Coordinator/health

# Проверка воркера
curl http://localhost:6001/api/Computation/health

# Список зарегистрированных воркеров
curl http://localhost:5000/api/Coordinator/workers
```

### 3. Подключение клиента

```bash
dotnet run --project DistributedSolver.Client
```

Или запустите `DistributedSolver.Client\Launch_Client.bat`.

В интерфейсе:
1. Нажмите **"Workers"** для получения списка воркеров с координатора
2. Загрузите данные или сгенерируйте матрицу
3. Запустите нужный сценарий (распределённый/линейный/сравнение)

## Архитектура сервисов

| Сервис | Порт | Описание |
|--------|------|----------|
| `coordinator` | 5000 | ASP.NET Core API координатор |
| `worker1` | 6001 | Вычислительный узел |
| `worker2` | 6002 | Вычислительный узел |
| `worker3` | 6003 | Вычислительный узел |
| `worker4` | 6004 | Вычислительный узел |
| `worker5` | 6005 | Вычислительный узел |
| `worker6` | 6006 | Вычислительный узел |

## Управление контейнерами

### Просмотр логов

```bash
# Все сервисы
docker-compose logs -f

# Конкретный сервис
docker-compose logs -f coordinator
docker-compose logs -f worker3
```

### Перезапуск сервисов

```bash
# Перезапуск координатора
docker-compose restart coordinator

# Перезапуск всех воркеров
docker-compose restart worker1 worker2 worker3 worker4 worker5 worker6

# Перезапуск всего стенда
docker-compose restart
```

### Остановка и очистка

```bash
# Остановить все контейнеры
docker-compose down

# Остановить и удалить volumes
docker-compose down -v

# Остановить и удалить образы
docker-compose down --rmi all
```

### Добавление нового воркера

1. Откройте `docker-compose.yml`
2. Скопируйте блок `worker6` и создайте `worker7`:

```yaml
worker7:
  build:
    context: .
    dockerfile: DistributedSolver.Worker/Dockerfile
  container_name: distributed-solver-worker7
  environment:
    - WORKER_PORT=6007
    - COORDINATOR_URL=http://coordinator:5000
  ports:
    - "6007:6007"
  networks:
    - distributed-solver-network
  depends_on:
    - coordinator
```

3. Запустите новый воркер:

```bash
docker-compose up -d worker7
```

## Автоматическая регистрация

Каждый воркер автоматически регистрируется на координаторе при запуске и периодически повторяет попытки при неудаче. Порядок запуска сервисов не критичен благодаря механизму повторных попыток.

## Типовые проблемы

### Воркеры не появляются в списке

1. Проверьте статус контейнеров:
   ```bash
   docker-compose ps
   ```

2. Проверьте логи воркера:
   ```bash
   docker-compose logs worker1
   ```

3. Убедитесь, что координатор отвечает:
   ```bash
   curl http://localhost:5000/api/Coordinator/health
   ```

4. Проверьте переменные окружения воркера:
   ```bash
   docker-compose exec worker1 env | grep COORDINATOR
   ```

### Порты заняты

Если порты `5000` или `6001-6006` заняты:

1. Измените маппинг портов в `docker-compose.yml`:
   ```yaml
   ports:
     - "5001:5000"  # Внешний:Внутренний
   ```

2. Или остановите конфликтующие процессы:
   ```bash
   # Windows
   netstat -ano | findstr :5000
   taskkill /PID <PID> /F
   ```

### Сборка падает

1. Убедитесь, что установлен Docker Desktop и он запущен
2. Проверьте версию .NET SDK в Dockerfile (должна быть 8.0)
3. Попробуйте собрать локально:
   ```bash
   dotnet build DistributedSolver.sln
   ```

### Проблемы с сетью

Если контейнеры не могут связаться друг с другом:

1. Проверьте, что все сервисы в одной сети:
   ```bash
   docker network inspect distributed-solver-network
   ```

2. Пересоздайте сеть:
   ```bash
   docker-compose down
   docker network rm distributed-solver-network
   docker-compose up
   ```

## Мониторинг

### Использование ресурсов

```bash
docker stats
```

### Проверка здоровья всех сервисов

```bash
# Координатор
curl http://localhost:5000/api/Coordinator/health

# Все воркеры
for port in 6001 6002 6003 6004 6005 6006; do
  echo "Worker $port:"
  curl -s http://localhost:$port/api/Computation/health | jq
done
```

## Production-готовность

Для production-развертывания рекомендуется:

1. Использовать внешнюю базу данных для хранения задач (если требуется персистентность)
2. Настроить логирование в централизованную систему (ELK, Seq и т.д.)
3. Добавить мониторинг (Prometheus + Grafana)
4. Настроить health checks в docker-compose.yml
5. Использовать секреты для конфигурации вместо переменных окружения в compose-файле
