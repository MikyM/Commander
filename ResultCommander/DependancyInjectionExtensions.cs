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
    /// <param name="configuration">Optional <see cref="ResultCommanderConfiguration"/> configuration.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, Action<ResultCommanderConfiguration>? configuration = null)
    {
        var config = new ResultCommanderConfiguration(builder);
        configuration?.Invoke(config);

        var iopt = Options.Create(config);

        builder.RegisterInstance(iopt).As<IOptions<ResultCommanderConfiguration>>().SingleInstance();
        builder.Register(x => x.Resolve<IOptions<ResultCommanderConfiguration>>().Value).As<ResultCommanderConfiguration>().SingleInstance();

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
}
