# Uge 39 — Labs

## Task 01: Writing a Dockerfile for your microservice

**Krav for gennemførelse** — Færdig: Se

### Writing a Dockerfile for your microservice

The goal of this assignment is to create a Dockerfile that can create a docker image of your microservice.

### The Dockerfile

A Dockerfile is a set of instructions for Docker to build an image of your microservice. The instructions describe the base image to use, the commands to run to install dependencies, the commands to run to build the application, and the commands to run to run the application.

#### Base image

The first thing you need to do is to define the base image of your microservice. The base image should contain all that you need to run the application. I the case of a .NET application, the base image should contain the .NET runtime.

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
```

The `WORKDIR` command sets the default working directory inside the container and `EXPOSE` allows us to listen on a specific port.

#### The build stage

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["apps/sample-microservice/src/SampleMicroservice.Presentation/SampleMicroservice.Presentation.csproj", "apps/sample-microservice/src/SampleMicroservice.Presentation/"]
COPY ["packages/MessageClient/src/MessageClient.csproj", "../../packages/MessageClient/src"]
COPY ["packages/Messages/Messages.csproj", "../../packages/Messages"]
...
RUN dotnet restore "apps/sample-microservice/src/SampleMicroservice.Presentation/SampleMicroservice.Presentation.csproj"
```

Here you have to copy in the official .NET SDK image. This is needed to compile the application code. Optionally you can set the `BUILD_CONFIGURATION` argument to `Release` to build the application in release mode.

Next, we set the working directory to `/src` and copy:

- The microservice `.csproj` file
- All of the `.csproj` files of the dependencies. In this example the dependencies are `MessageClient` and `Messages`.

Lastly, running the `dotnet restore` command will fetch all of the required dependencies before the main build.

#### Project structure

Copying files over, we want to maintain the directory structure of the monorepo to avoid breaking any project references.

#### Copy, build and output

```dockerfile
WORKDIR /src
COPY ["apps/sample-microservice/src/SampleMicroservice.Presentation/SampleMicroservice.Presentation.csproj", "apps/sample-microservice/src/SampleMicroservice.Presentation/"]
COPY ["packages/MessageClient/src/MessageClient.csproj", "../../packages/MessageClient/src"]
COPY ["packages/Messages/Messages.csproj", "../../packages/Messages"]
WORKDIR "apps/sample-microservice/src/SampleMicroservice.Presentation/"
RUN dotnet build "SampleMicroservice.Presentation.csproj" -c $BUILD_CONFIGURATION -o /app/build
```

Here we copy all of the project source files and set the working directory. Then we run the build.

#### The publish stage

```dockerfile
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SampleMicroservice.Presentation.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false
```

In the publish stage we are re-using the build image for running the `dotnet publish` command. The publish command optimizes build outputs, producing files that are ready for deployment.

Setting the `-o` flag to `/app/publish` will output the files to the `/app/publish` directory.

The `/p:UseAppHost=false` flag is used to prevent the default behavior of the `dotnet publish` command, which is to create a self-contained deployment that includes the .NET runtime.

#### The final image

```dockerfile
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Bizcord.SampleMicroservice.Presentation.dll"]
```

Finally, we use the official ASP.NET 7.0 runtime image again as our final container image. We set the working directory to `/app` and then copy over the published output from the publish stage.

By specifying `ENTRYPOINT ["dotnet", "Bizcord.SampleMicroservice.Presentation.dll"]`, we tell Docker to run the SampleMicroservice application when the container starts. This results in a clean, production-ready image that can be easily deployed and run.

---

## Task 02: Writing a Docker compose file for your microservice

**Krav for gennemførelse** — Færdig: Se

### Writing a Docker Compose file for your microservice

#### Overview

In this assignment, you will learn how to set up a Docker Compose file that starts two services:

1. A RabbitMQ service (for messaging).
2. Your .NET microservice (the one you containerized in the previous assignment).

Your goal is to ensure RabbitMQ is fully running and healthy before your microservice starts, preventing any "connection refused" errors due to RabbitMQ not being ready yet.

#### Task Steps

1. **Create a `docker-compose.yml` File**
   - Initialize a new `docker-compose.yml` in the root of your microservice project.
   - Reference the sample Compose file in this assignment for guidance.

2. **Configure the RabbitMQ Service**
   - Use the `rabbitmq:3-management` image, which includes a web-based admin interface.
   - Expose ports `5672` (RabbitMQ's AMQP port) and `15672` (the management UI).
   - Add a health check so Docker can verify that RabbitMQ is ready.

3. **Configure Your Microservice Service**
   - Point to your .NET microservice's Dockerfile using the `build` property.
   - Add a `depends_on` section to ensure your microservice waits for RabbitMQ's health check to pass.
   - Expose and map the port you intend to use (e.g., `80` in the container to `8000` on your machine).
   - Pass an environment variable (e.g., `RABBITMQ_HOST=[YOUR_RABBITMQ_HOST]`) that your microservice can read to connect to RabbitMQ.

4. **RabbitMQ health check**
   - Add a health check to the RabbitMQ service to ensure it is ready before your microservice starts.

#### Example `docker-compose.yml`

Below is a sample Compose file to guide you. Adjust ports, filenames, and paths as needed for your project.

```yaml
version: "3.8"

services:
  rabbitmq:
    image: rabbitmq:3-management
    container_name: "rabbitmq"
    ports:
      - "5672:5672"
      - "15672:15672"

  samplemicroservice:
    build:
      context: .
      dockerfile: projects/SampleMicroservice/Dockerfile
    depends_on:
      rabbitmq:
        condition: service_healthy
    ports:
      - "8000:80"
    environment:
      - "RABBITMQ_HOST=[YOUR_RABBITMQ_HOST]"
```

#### Explanation of key sections

- **`rabbitmq` service**
  - **Image:** Uses `rabbitmq:3-management` image, which includes a web-based admin interface.
  - **Ports:** Exposes ports `5672` (RabbitMQ's AMQP port) and `15672` (the management UI).

- **`samplemicroservice` service**
  - **Build:** Points to your .NET microservice's Dockerfile using the `build` property.
  - **Depends on:** Ensures your microservice waits for RabbitMQ's health check to pass.
  - **Ports:** Exposes and maps the port you intend to use (e.g., `80` in the container to `8000` on your machine).
  - **Environment:** Passes an environment variable (e.g., `RABBITMQ_HOST=[YOUR_RABBITMQ_HOST]`) that your microservice can read to connect to RabbitMQ.

#### Depends on

> `depends_on` only ensures that the service is started before the microservice. It does not ensure that the service is ready. To ensure that the service is ready, you can use a health check.

#### What you need to figure out

1. **How your .NET microservice reads the `RABBITMQ_HOST` environment variable**
   You can use either `appsettings.json` or use environment variable binding in your code.

2. **How to configure the health check for RabbitMQ**
   Does the `rabbitmq:3-management` image support a health check?
