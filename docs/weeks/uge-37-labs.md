# Uge 37 - Implementation Labs

## Lab 1: Setup your microservice

The goal of this first task is to get your repository set up and ready for development.

### Branching strategy

You will be working in your own repository for the microservice your team is responsible for. The `main` branch is the single source of truth and should always be in a stable and deployable state.

For new features, fixes, and other changes, work on short-lived branches and merge them back into `main` when they are ready. This keeps changes small and makes it easier to review each other's work. Use clear branch names that describe what you are working on, for example:

```bash
feat/add-discount-handling
fix/fix-jwt-validation
chore/cleanup-unused-code
```

### Setup your microservice

1. Create a new branch for setting up the project: `chore/initialize-project`
2. Initialize the project in the root of your repository.
3. Use the following basic structure:

```bash
src/
tests/
Dockerfile
README.md
```

### Your repository is deployable

Your repository represents a single deployable microservice. It should be dockerized and able to run independently from the other microservices. As a starting point, create an empty Dockerfile.

---

## Lab 2: Boilerplate code

The goal of this task is to add boilerplate code to your microservice.

### Initializing a .NET project

For .NET the boilerplate code can be based on [creating a web API project](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api?view=aspnetcore-7.0&tabs=visual-studio).

This will generate a project containing a sample REST API, but it doesn't comply with the project structure we have in the repository.

### Project structure

Make changes to your project to comply with the following structure:

```bash
apps/
  [microservice-domain]-microservice/
    src/
    tests/
    Dockerfile
    README.md
```

---

## Lab 3: Implement a message client

### Feature: Message client

In this task you'll implement a message client for you microservice. The purpose of the message client is to encapsulate the `EasyNetQ` library by providing a simple interface and a way of swapping out the message broker, if needed.

### Message Client Interface

First, implement the interface for the message client. The interface should be called `IMessageClient`. It's your task to design the interface - what methods and properties should it have?

- Publish
- Subscribe
- ..?

### Abstracting RabbitMQ

The goal here is to abstract away the details of RabbitMQ. We should ideally be able to swap out RabbitMQ with e.g. Kafka by implementing a simple adapter.

### Implement the message client

The implementation of the message client should be based on the `EasyNetQ` library. Implement the message client such that it can be easily used within your microservice.

One way of making the message client easy to integrate into you microservice is to keep the interface simple and hide the complexities of the underlying message broker and it's configuration.

Besides the interface you may also want to implement a service collection extension method for easily registering (DI) the message client in your application.

If you need a practical example of how `EasyNetQ` is used, your can refer to the [code](https://github.com/patrickstolc/dotnet-web-api-and-rabbitmq/tree/main) from this weeks lecture.
