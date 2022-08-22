using System.Reflection;
using Autofac;
using Autofac.Extras.DynamicProxy;
using Autofac.Features.Decorators;
using Microsoft.Extensions.Options;
using MikyM.Autofac.Extensions;
using MikyM.Autofac.Extensions.Attributes;
using MikyM.Common.Utilities.Extensions;

namespace ResultCommander;

/// <summary>
/// DI extensions for <see cref="ContainerBuilder"/>.
/// </summary>
[PublicAPI]
public static class DependancyInjectionExtensions
{
    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="configuration">Optional <see cref="CommanderConfiguration"/> configuration.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddCommandHandlers(this ContainerBuilder builder, Action<CommanderConfiguration>? configuration = null)
    {
        var config = new CommanderConfiguration(builder);
        configuration?.Invoke(config);

        builder.Register(x => config).As<IOptions<CommanderConfiguration>>().SingleInstance();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var commandSet = assembly.GetTypes()
                .Where(x => x.GetInterfaces().Any(y => y.IsGenericType && y.GetGenericTypeDefinition() == typeof(IAsyncCommandHandler<>)) && x.IsClass && !x.IsAbstract)
                .ToList();

            var commandResultSet = assembly.GetTypes()
                .Where(x => x.GetInterfaces().Any(y => y.IsGenericType && y.GetGenericTypeDefinition() == typeof(IAsyncCommandHandler<,>)) && x.IsClass && !x.IsAbstract)
                .ToList();

            var commandSubSet = commandSet
                .Where(x => (x.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
                            x.GetCustomAttributes<InterceptedByAttribute>(false).Any()) && x.IsClass &&
                            !x.IsAbstract)
                .ToList();

            var commandResultSubSet = commandResultSet
                .Where(x => (x.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
                            x.GetCustomAttributes<InterceptedByAttribute>(false).Any()) && x.IsClass &&
                            !x.IsAbstract)
                .ToList();
            
            var syncCommandSet = assembly.GetTypes()
                .Where(x => x.GetInterfaces().Any(y => y.IsGenericType && y.GetGenericTypeDefinition() == typeof(ISyncCommandHandler<>)) && x.IsClass && !x.IsAbstract)
                .ToList();

            var syncCommandResultSet = assembly.GetTypes()
                .Where(x => x.GetInterfaces().Any(y => y.IsGenericType && y.GetGenericTypeDefinition() == typeof(ISyncCommandHandler<,>)) && x.IsClass && !x.IsAbstract)
                .ToList();

            var syncCommandSubSet = commandSet
                .Where(x => (x.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
                             x.GetCustomAttributes<InterceptedByAttribute>(false).Any()) && x.IsClass &&
                            !x.IsAbstract)
                .ToList();

            var syncCommandResultSubSet = commandResultSet
                .Where(x => (x.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
                             x.GetCustomAttributes<InterceptedByAttribute>(false).Any()) && x.IsClass &&
                            !x.IsAbstract)
                .ToList();

            foreach (var type in commandSubSet)
            {
                var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

                var registrationBuilder = builder.RegisterTypes(type).AsClosedInterfacesOf(typeof(IAsyncCommandHandler<>));

                var scope = lifeAttr?.Scope ?? config.DefaultHandlerLifetime;

                switch (scope)
                {
                    case Lifetime.SingleInstance:
                        registrationBuilder = registrationBuilder.SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        registrationBuilder = registrationBuilder.InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        registrationBuilder = registrationBuilder.InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerDependency:
                        registrationBuilder = registrationBuilder.InstancePerDependency();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        registrationBuilder =
                            registrationBuilder.InstancePerMatchingLifetimeScope(lifeAttr?.Tags.ToArray() ?? throw new InvalidOperationException());
                        break;
                    case Lifetime.InstancePerOwned:
                        if (lifeAttr?.Owned is null) throw new InvalidOperationException("Owned type was null");

                        registrationBuilder = registrationBuilder.InstancePerOwned(lifeAttr.Owned);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }


                var intrAttr = type.GetCustomAttribute<EnableInterceptionAttribute>(false);
                if (intrAttr is null) 
                    continue;

                var intrAttrs = type.GetCustomAttributes<InterceptedByAttribute>(false);

                foreach (var attr in intrAttrs)
                {
                    registrationBuilder = registrationBuilder.EnableInterfaceInterceptors();
                    registrationBuilder = attr.IsAsync
                        ? registrationBuilder.InterceptedBy(
                            typeof(AsyncInterceptorAdapter<>).MakeGenericType(attr.Interceptor))
                        : registrationBuilder.InterceptedBy(attr.Interceptor);
                }
            }

            foreach (var type in commandResultSubSet)
            {
                var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

                var registrationBuilder = builder.RegisterTypes(type).AsClosedInterfacesOf(typeof(IAsyncCommandHandler<,>));

                var scope = lifeAttr?.Scope ?? config.DefaultHandlerLifetime;

                switch (scope)
                {
                    case Lifetime.SingleInstance:
                        registrationBuilder = registrationBuilder.SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        registrationBuilder = registrationBuilder.InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        registrationBuilder = registrationBuilder.InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerDependency:
                        registrationBuilder = registrationBuilder.InstancePerDependency();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        registrationBuilder =
                            registrationBuilder.InstancePerMatchingLifetimeScope(lifeAttr?.Tags.ToArray() ?? throw new InvalidOperationException());
                        break;
                    case Lifetime.InstancePerOwned:
                        if (lifeAttr?.Owned is null) throw new InvalidOperationException("Owned type was null");

                        registrationBuilder = registrationBuilder.InstancePerOwned(lifeAttr.Owned);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var intrAttr = type.GetCustomAttribute<EnableInterceptionAttribute>(false);
                if (intrAttr is null) 
                    continue;

                if (intrAttr.Intercept is not (Intercept.Interface or Intercept.InterfaceAndClass))
                    throw new NotSupportedException("Only interface interception is supported for command handlers");
                
                var intrAttrs = type.GetCustomAttributes<InterceptedByAttribute>(false);

                foreach (var attr in intrAttrs)
                {
                    registrationBuilder = registrationBuilder.EnableInterfaceInterceptors();
                    registrationBuilder = attr.IsAsync
                        ? registrationBuilder.InterceptedBy(
                            typeof(AsyncInterceptorAdapter<>).MakeGenericType(attr.Interceptor))
                        : registrationBuilder.InterceptedBy(attr.Interceptor);
                }
            }

            commandSet.RemoveAll(x => commandSubSet.Any(y => y == x));
            commandResultSet.RemoveAll(x => commandResultSubSet.Any(y => y == x));

            if (commandSet.Any())
            {
                switch (config.DefaultHandlerLifetime)
                {
                    case Lifetime.SingleInstance:
                        builder.RegisterTypes(commandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<>))
                            .SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        builder.RegisterTypes(commandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<>))
                            .InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        builder.RegisterTypes(commandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<>))
                            .InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        throw new NotSupportedException();
                    case Lifetime.InstancePerDependency:
                        builder.RegisterTypes(commandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<>))
                            .InstancePerDependency();
                        break;
                    case Lifetime.InstancePerOwned:
                        throw new NotSupportedException();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            if (commandResultSet.Any())
            {
                switch (config.DefaultHandlerLifetime)
                {
                    case Lifetime.SingleInstance:
                        builder.RegisterTypes(commandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<,>))
                            .SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        builder.RegisterTypes(commandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<,>))
                            .InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        builder.RegisterTypes(commandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<,>))
                            .InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        throw new NotSupportedException();
                    case Lifetime.InstancePerDependency:
                        builder.RegisterTypes(commandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(IAsyncCommandHandler<,>))
                            .InstancePerDependency();
                        break;
                    case Lifetime.InstancePerOwned:
                        throw new NotSupportedException();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            foreach (var type in syncCommandSubSet)
            {
                var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

                var registrationBuilder = builder.RegisterTypes(type).AsClosedInterfacesOf(typeof(ISyncCommandHandler<>));

                var scope = lifeAttr?.Scope ?? config.DefaultHandlerLifetime;

                switch (scope)
                {
                    case Lifetime.SingleInstance:
                        registrationBuilder = registrationBuilder.SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        registrationBuilder = registrationBuilder.InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        registrationBuilder = registrationBuilder.InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerDependency:
                        registrationBuilder = registrationBuilder.InstancePerDependency();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        registrationBuilder =
                            registrationBuilder.InstancePerMatchingLifetimeScope(lifeAttr?.Tags.ToArray() ?? throw new InvalidOperationException());
                        break;
                    case Lifetime.InstancePerOwned:
                        if (lifeAttr?.Owned is null) throw new InvalidOperationException("Owned type was null");

                        registrationBuilder = registrationBuilder.InstancePerOwned(lifeAttr.Owned);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }


                var intrAttr = type.GetCustomAttribute<EnableInterceptionAttribute>(false);
                if (intrAttr is null) 
                    continue;

                var intrAttrs = type.GetCustomAttributes<InterceptedByAttribute>(false);

                foreach (var attr in intrAttrs)
                {
                    registrationBuilder = registrationBuilder.EnableInterfaceInterceptors();
                    registrationBuilder = attr.IsAsync
                        ? registrationBuilder.InterceptedBy(
                            typeof(AsyncInterceptorAdapter<>).MakeGenericType(attr.Interceptor))
                        : registrationBuilder.InterceptedBy(attr.Interceptor);
                }
            }

            foreach (var type in syncCommandResultSubSet)
            {
                var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

                var registrationBuilder = builder.RegisterTypes(type).AsClosedInterfacesOf(typeof(ISyncCommandHandler<,>));

                var scope = lifeAttr?.Scope ?? config.DefaultHandlerLifetime;

                switch (scope)
                {
                    case Lifetime.SingleInstance:
                        registrationBuilder = registrationBuilder.SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        registrationBuilder = registrationBuilder.InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        registrationBuilder = registrationBuilder.InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerDependency:
                        registrationBuilder = registrationBuilder.InstancePerDependency();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        registrationBuilder =
                            registrationBuilder.InstancePerMatchingLifetimeScope(lifeAttr?.Tags.ToArray() ?? throw new InvalidOperationException());
                        break;
                    case Lifetime.InstancePerOwned:
                        if (lifeAttr?.Owned is null) throw new InvalidOperationException("Owned type was null");

                        registrationBuilder = registrationBuilder.InstancePerOwned(lifeAttr.Owned);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var intrAttr = type.GetCustomAttribute<EnableInterceptionAttribute>(false);
                if (intrAttr is null) 
                    continue;

                if (intrAttr.Intercept is not (Intercept.Interface or Intercept.InterfaceAndClass))
                    throw new NotSupportedException("Only interface interception is supported for command handlers");
                
                var intrAttrs = type.GetCustomAttributes<InterceptedByAttribute>(false);

                foreach (var attr in intrAttrs)
                {
                    registrationBuilder = registrationBuilder.EnableInterfaceInterceptors();
                    registrationBuilder = attr.IsAsync
                        ? registrationBuilder.InterceptedBy(
                            typeof(AsyncInterceptorAdapter<>).MakeGenericType(attr.Interceptor))
                        : registrationBuilder.InterceptedBy(attr.Interceptor);
                }
            }

            syncCommandSet.RemoveAll(x => syncCommandSubSet.Any(y => y == x));
            syncCommandResultSet.RemoveAll(x => syncCommandResultSubSet.Any(y => y == x));

            if (syncCommandSet.Any())
            {
                switch (config.DefaultHandlerLifetime)
                {
                    case Lifetime.SingleInstance:
                        builder.RegisterTypes(syncCommandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<>))
                            .SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        builder.RegisterTypes(syncCommandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<>))
                            .InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        builder.RegisterTypes(syncCommandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<>))
                            .InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        throw new NotSupportedException();
                    case Lifetime.InstancePerDependency:
                        builder.RegisterTypes(syncCommandSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<>))
                            .InstancePerDependency();
                        break;
                    case Lifetime.InstancePerOwned:
                        throw new NotSupportedException();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            if (syncCommandResultSet.Any())
            {
                switch (config.DefaultHandlerLifetime)
                {
                    case Lifetime.SingleInstance:
                        builder.RegisterTypes(syncCommandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<,>))
                            .SingleInstance();
                        break;
                    case Lifetime.InstancePerRequest:
                        builder.RegisterTypes(syncCommandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<,>))
                            .InstancePerRequest();
                        break;
                    case Lifetime.InstancePerLifetimeScope:
                        builder.RegisterTypes(syncCommandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<,>))
                            .InstancePerLifetimeScope();
                        break;
                    case Lifetime.InstancePerMatchingLifetimeScope:
                        throw new NotSupportedException();
                    case Lifetime.InstancePerDependency:
                        builder.RegisterTypes(syncCommandResultSet.ToArray())
                            .AsClosedInterfacesOf(typeof(ISyncCommandHandler<,>))
                            .InstancePerDependency();
                        break;
                    case Lifetime.InstancePerOwned:
                        throw new NotSupportedException();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        
        switch (config.DefaultHandlerFactoryLifetime)
        {
            case Lifetime.SingleInstance:
                builder.RegisterType<CommandHandlerFactory>().As<ICommandHandlerFactory>().SingleInstance();
                break;
            case Lifetime.InstancePerRequest:
                builder.RegisterType<CommandHandlerFactory>().As<ICommandHandlerFactory>().InstancePerRequest();
                break;
            case Lifetime.InstancePerLifetimeScope:
                builder.RegisterType<CommandHandlerFactory>().As<ICommandHandlerFactory>().InstancePerLifetimeScope();
                break;
            case Lifetime.InstancePerMatchingLifetimeScope:
                throw new NotSupportedException();
            case Lifetime.InstancePerDependency:
                builder.RegisterType<CommandHandlerFactory>().As<ICommandHandlerFactory>().InstancePerDependency();
                break;
            case Lifetime.InstancePerOwned:
                throw new NotSupportedException();
            default:
                throw new ArgumentOutOfRangeException();
        }

        return builder;
    }

    /// <summary>
    /// Registers a decorator for command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <param name="condition">Condition to decide whether the decorator should be applied.</param>
    /// <returns>Current <see cref="CommanderConfiguration"/> instance.</returns>
    public static CommanderConfiguration AddDecorator<TDecorator>(this CommanderConfiguration options, Func<IDecoratorContext, bool>? condition = null) where TDecorator : ICommandHandlerBase
    {
        if (typeof(TDecorator).IsGenericType || typeof(TDecorator).IsGenericTypeDefinition)
            throw new NotSupportedException("Given decorator type is a generic type, use AddGenericDecorator method instead");
        
        if (typeof(TDecorator).IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<>)))
            options.Builder.RegisterDecorator(typeof(TDecorator), typeof(IAsyncCommandHandler<>), condition);
        else if (typeof(TDecorator).IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<,>)))
            options.Builder.RegisterDecorator(typeof(TDecorator), typeof(IAsyncCommandHandler<,>), condition);
        else if (typeof(TDecorator).IsAssignableToWithGenerics(typeof(ISyncCommandHandler<>)))
            options.Builder.RegisterDecorator(typeof(TDecorator), typeof(ISyncCommandHandler<>), condition);
        else if (typeof(TDecorator).IsAssignableToWithGenerics(typeof(ISyncCommandHandler<,>)))
            options.Builder.RegisterDecorator(typeof(TDecorator), typeof(ISyncCommandHandler<,>), condition);
        else
            throw new NotSupportedException("Given decorator type can't decorate any command handler");

        return options;
    }
    
    /// <summary>
    /// Registers a decorator for command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <param name="condition">Condition to decide whether the decorator should be applied.</param>
    /// <returns>Current <see cref="CommanderConfiguration"/> instance.</returns>
    public static CommanderConfiguration AddDecorator<TDecorator, TDecoratedHandler>(this CommanderConfiguration options, Func<IDecoratorContext, bool>? condition = null) where TDecorator : ICommandHandlerBase where TDecoratedHandler : ICommandHandlerBase
    {
        if (typeof(TDecorator).IsGenericType || typeof(TDecorator).IsGenericTypeDefinition)
            throw new NotSupportedException("Given decorator type is a generic type, use AddGenericDecorator method instead");
        if (typeof(TDecoratedHandler).IsGenericType || typeof(TDecoratedHandler).IsGenericTypeDefinition)
            throw new NotSupportedException("You must use concrete handler implementation type");
        
        if (typeof(TDecorator).IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<>)) &&
            typeof(TDecoratedHandler).IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<>)))
        {
            Func<IDecoratorContext, bool> predicate = x => x.ServiceType == typeof(TDecoratedHandler);
            if (condition is not null)
                predicate += condition;
            
            options.Builder.RegisterDecorator(typeof(TDecorator), typeof(IAsyncCommandHandler<>), predicate);
        }
        else if (typeof(TDecorator).IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<,>)) &&
            typeof(TDecoratedHandler).IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<,>)))
        {
            Func<IDecoratorContext, bool> predicate = x => x.ServiceType == typeof(TDecoratedHandler);
            if (condition is not null)
                predicate += condition;
            
            options.Builder.RegisterDecorator(typeof(TDecorator), typeof(IAsyncCommandHandler<,>), predicate);
        }
        else if (typeof(TDecorator).IsAssignableToWithGenerics(typeof(ISyncCommandHandler<>)) &&
            typeof(TDecoratedHandler).IsAssignableToWithGenerics(typeof(ISyncCommandHandler<>)))
        {
            Func<IDecoratorContext, bool> predicate = x => x.ServiceType == typeof(TDecoratedHandler);
            if (condition is not null)
                predicate += condition;
            
            options.Builder.RegisterDecorator(typeof(TDecorator), typeof(ISyncCommandHandler<>), predicate);
        }
        else if (typeof(TDecorator).IsAssignableToWithGenerics(typeof(ISyncCommandHandler<,>)) &&
                 typeof(TDecoratedHandler).IsAssignableToWithGenerics(typeof(ISyncCommandHandler<,>)))
        {
            Func<IDecoratorContext, bool> predicate = x => x.ServiceType == typeof(TDecoratedHandler);
            if (condition is not null)
                predicate += condition;
            
            options.Builder.RegisterDecorator(typeof(TDecorator), typeof(ISyncCommandHandler<,>), predicate);
        }
        else
            throw new NotSupportedException("Given decorator type can't decorate given command handler");

        return options;
    }

    /// <summary>
    /// Registers a generic decorator for command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <param name="decoratorType">Decorator type.</param>
    /// <param name="condition">Condition to decide whether the decorator should be applied.</param>
    /// <returns>Current <see cref="CommanderConfiguration"/> instance</returns>
    public static CommanderConfiguration AddGenericDecorator(this CommanderConfiguration options, Type decoratorType, Func<IDecoratorContext, bool>? condition = null)
    {
        if (!decoratorType.IsGenericType && !decoratorType.IsGenericTypeDefinition)
            throw new NotSupportedException("Given decorator type is not a generic type");
        
        if (decoratorType.IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<>)))
            options.Builder.RegisterGenericDecorator(decoratorType, typeof(IAsyncCommandHandler<>), condition);
        else if (decoratorType.IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<,>)))
            options.Builder.RegisterGenericDecorator(decoratorType, typeof(IAsyncCommandHandler<,>), condition);
        else if (decoratorType.IsAssignableToWithGenerics(typeof(ISyncCommandHandler<>)))
            options.Builder.RegisterGenericDecorator(decoratorType, typeof(ISyncCommandHandler<>), condition);
        else if (decoratorType.IsAssignableToWithGenerics(typeof(ISyncCommandHandler<,>)))
            options.Builder.RegisterGenericDecorator(decoratorType, typeof(ISyncCommandHandler<,>), condition);
        else
            throw new NotSupportedException("Given decorator type can't decorate any command handler");

        return options;
    }
    
    /// <summary>
    /// Registers a decorator for command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <param name="decoratorType">Decorator type.</param>
    /// <param name="condition">Condition to decide whether the decorator should be applied.</param>
    /// <returns>Current <see cref="CommanderConfiguration"/> instance.</returns>
    public static CommanderConfiguration AddGenericDecorator<TDecoratedHandler>(this CommanderConfiguration options, Type decoratorType, Func<IDecoratorContext, bool>? condition = null) where TDecoratedHandler : ICommandHandlerBase
    {
        if (!decoratorType.IsGenericType && !decoratorType.IsGenericTypeDefinition)
            throw new NotSupportedException("Given decorator type is not a generic type");
        if (typeof(TDecoratedHandler).IsGenericType || typeof(TDecoratedHandler).IsGenericTypeDefinition)
            throw new NotSupportedException("You must use concrete handler implementation type");
        
        if (decoratorType.IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<>)) &&
            typeof(TDecoratedHandler).IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<>)))
        {
            Func<IDecoratorContext, bool> predicate = x => x.ServiceType == typeof(TDecoratedHandler);
            if (condition is not null)
                predicate += condition;
            
            options.Builder.RegisterDecorator(decoratorType, typeof(IAsyncCommandHandler<>), predicate);
        }
        else if (decoratorType.IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<,>)) &&
                typeof(TDecoratedHandler).IsAssignableToWithGenerics(typeof(IAsyncCommandHandler<,>)))
        {
            Func<IDecoratorContext, bool> predicate = x => x.ServiceType == typeof(TDecoratedHandler);
            if (condition is not null)
                predicate += condition;
            
            options.Builder.RegisterDecorator(decoratorType, typeof(IAsyncCommandHandler<,>), predicate);
        }
        else if (decoratorType.IsAssignableToWithGenerics(typeof(ISyncCommandHandler<>)) &&
            typeof(TDecoratedHandler).IsAssignableToWithGenerics(typeof(ISyncCommandHandler<>)))
        {
            Func<IDecoratorContext, bool> predicate = x => x.ServiceType == typeof(TDecoratedHandler);
            if (condition is not null)
                predicate += condition;
            
            options.Builder.RegisterDecorator(decoratorType, typeof(ISyncCommandHandler<>), predicate);
        }
        else if (decoratorType.IsAssignableToWithGenerics(typeof(ISyncCommandHandler<,>)) &&
                 typeof(TDecoratedHandler).IsAssignableToWithGenerics(typeof(ISyncCommandHandler<,>)))
        {
            Func<IDecoratorContext, bool> predicate = x => x.ServiceType == typeof(TDecoratedHandler);
            if (condition is not null)
                predicate += condition;
            
            options.Builder.RegisterDecorator(decoratorType, typeof(ISyncCommandHandler<,>), predicate);
        }
        else
            throw new NotSupportedException("Given decorator type can't decorate given command handler");

        return options;
    }

    /// <summary>
    /// Registers an adapter for command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <param name="adapter">Func that used to adapt service to another.</param>
    /// <returns>Current <see cref="CommanderConfiguration"/> instance.</returns>
    public static CommanderConfiguration AddAdapter<TAdapter, THandler>(
        this CommanderConfiguration options, Func<THandler, TAdapter> adapter)
        where THandler : class, ICommandHandlerBase where TAdapter : notnull
    {
        options.Builder.RegisterAdapter(adapter);
        return options;
    }
}
