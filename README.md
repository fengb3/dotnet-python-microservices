# dotnet-python-microservices

这是一个使用 .NET Aspire 编排的微服务架构示例，展示了如何使用 Redis Stream 作为消息队列，在 ASP.NET 和 Python 应用之间进行通信。

This is a microservices architecture example orchestrated with .NET Aspire, demonstrating how to use Redis Stream as a message queue for communication between ASP.NET and Python applications.

## 架构概述 / Architecture Overview

该项目包含以下组件：

This project consists of the following components:

- **MicroservicesApp.AppHost**: .NET Aspire AppHost，负责编排所有服务 / .NET Aspire AppHost for orchestrating all services
- **MicroservicesApp.ApiService**: ASP.NET Web API，发布任务消息并接收处理结果 / ASP.NET Web API that publishes task messages and receives processing results
- **PythonWorker**: Python 工作进程，消费任务并发布结果 / Python worker process that consumes tasks and publishes results
- **MicroservicesApp.Web**: Blazor Web 前端（示例） / Blazor Web frontend (example)
- **Shared/Protos**: 共享的 Protobuf 消息定义 / Shared Protobuf message definitions
- **Redis**: 消息队列（通过 Docker 容器运行）/ Message queue (runs via Docker container)

## 技术栈 / Technology Stack

- **.NET 10** with Aspire for orchestration
- **Python 3.12+** for worker service
- **Redis Stream** for message queue
- **Protobuf** for message serialization
- **Docker** for containerization

## 消息流 / Message Flow

1. ASP.NET API 接收 HTTP POST 请求创建任务 / ASP.NET API receives HTTP POST request to create task
2. API 将任务消息（Protobuf 格式）发布到 Redis Stream `tasks` / API publishes task message (Protobuf format) to Redis Stream `tasks`
3. Python Worker 从 `tasks` Stream 消费消息 / Python Worker consumes messages from `tasks` Stream
4. Python Worker 处理任务（模拟工作 2 秒）/ Python Worker processes task (simulates work for 2 seconds)
5. Python Worker 将结果消息发布到 Redis Stream `results` / Python Worker publishes result message to Redis Stream `results`
6. ASP.NET API 的后台服务消费结果并记录日志 / ASP.NET API's background service consumes results and logs them

## 前置要求 / Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Python 3.12+](https://www.python.org/downloads/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [.NET Aspire workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling)

安装 .NET Aspire workload:
```bash
dotnet workload install aspire
```

## 快速开始 / Quick Start

### 1. 安装 Python 依赖 / Install Python Dependencies

```bash
cd PythonWorker
pip install -r requirements.txt
# 或者 / or
pip install redis protobuf
```

### 2. 构建解决方案 / Build Solution

```bash
dotnet build MicroservicesApp.sln
```

### 3. 运行应用 / Run Application

使用 .NET Aspire 启动所有服务（包括 Redis）:

Start all services (including Redis) using .NET Aspire:

```bash
dotnet run --project MicroservicesApp.AppHost
```

这将启动:
- Redis 容器
- ASP.NET API Service
- Python Worker
- Blazor Web Frontend
- Aspire Dashboard (用于监控)

This will start:
- Redis container
- ASP.NET API Service
- Python Worker
- Blazor Web Frontend
- Aspire Dashboard (for monitoring)

### 4. 测试消息流 / Test Message Flow

打开浏览器访问 Aspire Dashboard (通常在 http://localhost:15xxx)，或直接访问 API。

Open browser to Aspire Dashboard (typically at http://localhost:15xxx), or access API directly.

发送任务请求 / Send a task request:

```bash
curl -X POST http://localhost:5000/task \
  -H "Content-Type: application/json" \
  -d '{"taskType":"example","data":"Hello from API"}'
```

查看日志:
1. 在 Aspire Dashboard 中查看 API 和 Python Worker 的日志
2. API 日志会显示发布的任务
3. Python Worker 日志会显示处理的任务
4. API 日志会显示接收到的结果

View logs:
1. Check API and Python Worker logs in Aspire Dashboard
2. API logs will show published tasks
3. Python Worker logs will show processed tasks
4. API logs will show received results

## 项目结构 / Project Structure

```
dotnet-python-microservices/
├── MicroservicesApp.AppHost/        # Aspire 编排主机 / Aspire orchestration host
│   └── AppHost.cs                   # 服务配置 / Service configuration
├── MicroservicesApp.ApiService/     # ASP.NET Web API
│   ├── Program.cs                   # API endpoints 和消息处理 / API endpoints and message handling
│   └── MicroservicesApp.ApiService.csproj
├── MicroservicesApp.Web/            # Blazor 前端 / Blazor frontend
├── MicroservicesApp.ServiceDefaults/ # 共享服务配置 / Shared service configuration
├── PythonWorker/                    # Python 工作进程 / Python worker
│   ├── worker.py                    # 工作进程主程序 / Worker main program
│   ├── pyproject.toml              # Python 项目配置 / Python project config
│   └── messages_pb2.py             # 生成的 Protobuf 类 / Generated Protobuf classes
└── Shared/
    └── Protos/
        └── messages.proto          # Protobuf 消息定义 / Protobuf message definitions
```

## Protobuf 消息定义 / Protobuf Message Definitions

### TaskMessage (任务消息)

```protobuf
message TaskMessage {
  string task_id = 1;      // 任务 ID / Task ID
  string task_type = 2;    // 任务类型 / Task type
  string data = 3;         // 任务数据 / Task data
  int64 timestamp = 4;     // 时间戳 / Timestamp
}
```

### ResultMessage (结果消息)

```protobuf
message ResultMessage {
  string task_id = 1;      // 任务 ID / Task ID
  string status = 2;       // 状态 / Status
  string result = 3;       // 结果 / Result
  int64 timestamp = 4;     // 时间戳 / Timestamp
}
```

## 开发说明 / Development Notes

### 修改 Protobuf 定义 / Modifying Protobuf Definitions

如果修改了 `Shared/Protos/messages.proto`:

If you modify `Shared/Protos/messages.proto`:

1. 重新构建 .NET 项目（自动生成 C# 代码）:
   ```bash
   dotnet build MicroservicesApp.ApiService
   ```

2. 重新生成 Python 代码:
   ```bash
   cd PythonWorker
   python3 -m grpc_tools.protoc -I../Shared/Protos --python_out=. ../Shared/Protos/messages.proto
   ```

### 扩展 Python Worker / Scaling Python Worker

可以启动多个 Python Worker 实例来并行处理任务。每个 Worker 会自动加入 Redis 消费组。

You can start multiple Python Worker instances to process tasks in parallel. Each Worker will automatically join the Redis consumer group.

### 监控和调试 / Monitoring and Debugging

使用 Aspire Dashboard:
- 查看所有服务状态
- 查看日志
- 查看分布式跟踪
- 查看指标

Use Aspire Dashboard to:
- View all service statuses
- View logs
- View distributed traces
- View metrics

## 许可证 / License

MIT
