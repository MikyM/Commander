# Commander

[![Build Status](https://travis-ci.org/joemccann/dillinger.svg?branch=master)](https://travis-ci.org/joemccann/dillinger)

Library featuring a command handler pattern for both synchronous and asynchronous operations.

Utilizes Autofac thus Autofac is required.

## Features

- Synchronous and asynchronous command handler definitions
- Definitions and base implementations of commands
- Supports decorators and adapters via Autofac's methods

## Description

There are two command types - one that only returns a [Result](https://github.com/MikyM/MikyM.Common.Utilities/blob/master/MikyM.Common.Utilities/Results/Result.cs) and one that returns an additional entity contained within the [Result](https://github.com/MikyM/MikyM.Common.Utilities/blob/master/MikyM.Common.Utilities/Results/Result.cs).

Every handler must return a [Result](https://github.com/MikyM/MikyM.Common.Utilities/blob/master/MikyM.Common.Utilities/Results/Result.cs) struct which determines whether the operation succedeed or not, handlers may or may not return additional results contained within the Result struct - this is defined by the handled comand.

## Installation

Since the library utilizes Autofac, base Autofac configuration is required to use command handlers - [Autofac's docs](https://autofac.readthedocs.io/en/latest/index.html).

To register handlers with the DI container use the ContainerBuilder extension method provided by the library:

```csharp
builder.AddCommandHandlers();
```

To register decorators or adapters use the methods available on CommanderConfiguration like so:
```csharp
builder.AddCommandHandlers(options => 
{
    options.AddDecorator<FancyDecorator, SimpleHandler>();
});
```
You can register multiple decorators and they'll be applied in the order that you register them - read more at [Autofac's docs regarding decorators and adapters](https://autofac.readthedocs.io/en/latest/advanced/adapters-decorators.html).

## Example usage

A command without a concrete result:
```csharp
public SimpleCommand : ICommand
{
    public bool IsSuccess { get; }
    
    public SimpleCommand(bool isSuccess = true)
        => IsSuccess = isSuccess;
}
```

And a synchronous handler that handles it:
```csharp
public SimpleSyncCommandHandler : ISyncCommandHandler<SimpleCommand>
{
    Result Handle(SimpleCommand command)
    {
        if (command.IsSuccess)
            return Result.FromSuccess();
            
        return new InvalidOperationError();
    }
}
```

A command with a concrete result:
```csharp
public SimpleCommandWithConcreteResult : ICommand<int>
{
    public bool IsSuccess { get; }
    
    public SimpleCommand(bool isSuccess = true)
        => IsSuccess = isSuccess;
}
```

And a synchronous handler that handles it:
```csharp
public SimpleSyncCommandHandlerWithConcreteResult : ISyncCommandHandler<SimpleCommand, int>
{
    Result<int> Handle(SimpleCommand command)
    {
        if (command.IsSuccess)
            return 1;
            
        return new InvalidOperationError();
    }
}
```
