using System.Reflection;
using AttributeBasedRegistration.Attributes.Abstractions;
using AttributeBasedRegistration.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceLifetime = AttributeBasedRegistration.ServiceLifetime;

namespace ResultCommander;

/// <summary>
/// DI extensions for <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class ServiceCollectionExtensions
{
    internal static Type AsyncResultHandlerType = typeof(IAsyncCommandHandler<,>);
    internal static Type AsyncHandlerType = typeof(IAsyncCommandHandler<>);
    internal static Type SyncResultHandlerType = typeof(ISyncCommandHandler<,>);
    internal static Type SyncHandlerType = typeof(ISyncCommandHandler<>);
    
    internal static bool HasCustomAttributes(this Type type)
        => type.GetRegistrationAttributesOfType<ILifetimeAttribute>().Any() ||
           type.GetRegistrationAttributesOfType<IDecoratedByAttribute>().Any() ||
           type.GetRegistrationAttributesOfType<IInterceptedByAttribute>().Any() ||
           type.GetRegistrationAttributesOfType<IEnableInterceptionAttribute>().Any();

    internal static bool ShouldSkip(this Type type)
        => type.GetCustomAttributes(false).Any(x => x is ISkipHandlerRegistrationAttribute);
    
    internal static bool IsAnyAsyncHandler(this Type type)
            => (type.IsAsyncHandler() || type.IsAsyncResultHandler()) && !ShouldSkip(type);
    
    internal static bool IsAsyncHandler(this Type type)
        => type.GetInterfaces().Any(y =>
               y.IsGenericType && y.GetGenericTypeDefinition() == AsyncHandlerType) &&
           type.IsClass && !type.IsAbstract && !ShouldSkip(type);
    
    internal static bool IsAsyncResultHandler(this Type type)
        => type.GetInterfaces().Any(y =>
               y.IsGenericType && y.GetGenericTypeDefinition() == AsyncResultHandlerType) &&
           type.IsClass && !type.IsAbstract && !ShouldSkip(type);

    internal static bool IsAsyncResultCustomizedHandler(this Type type)
        => IsAsyncResultHandler(type) && HasCustomAttributes(type);
    
    internal static bool IsAsyncCustomizedHandler(this Type type)
        => IsAsyncHandler(type) && HasCustomAttributes(type);
    
    internal static bool IsAsyncResultNonCustomizedHandler(this Type type)
        => IsAsyncResultHandler(type) && !HasCustomAttributes(type);
    
    internal static bool IsAsyncNonCustomizedHandler(this Type type)
        => IsAsyncHandler(type) && !HasCustomAttributes(type);
    
    internal static bool IsAnySyncHandler(this Type type)
        => (type.IsSyncHandler() || type.IsSyncResultHandler()) && !ShouldSkip(type);
    
    internal static bool IsSyncHandler(this Type type)
        => type.GetInterfaces().Any(y =>
               y.IsGenericType && y.GetGenericTypeDefinition() == SyncHandlerType) &&
           type.IsClass && !type.IsAbstract && !ShouldSkip(type);
    
    internal static bool IsSyncResultHandler(this Type type)
        => type.GetInterfaces().Any(y =>
               y.IsGenericType && y.GetGenericTypeDefinition() == SyncResultHandlerType) &&
           type.IsClass && !type.IsAbstract && !ShouldSkip(type);

    internal static bool IsSyncResultCustomizedHandler(this Type type)
        => IsSyncResultHandler(type) && HasCustomAttributes(type);
    
    internal static bool IsSyncCustomizedHandler(this Type type)
        => IsSyncHandler(type) && HasCustomAttributes(type);
    
    internal static bool IsSyncResultNonCustomizedHandler(this Type type)
        => IsSyncResultHandler(type) && !HasCustomAttributes(type);
    
    internal static bool IsSyncNonCustomizedHandler(this Type type)
        => IsSyncHandler(type) && !HasCustomAttributes(type);

    internal static bool IsHandlerInterface(this Type type)
        => type.IsInterface && type.IsGenericType && (type.GetGenericTypeDefinition() == SyncHandlerType ||
                                  type.GetGenericTypeDefinition() == SyncResultHandlerType ||
                                  type.GetGenericTypeDefinition() == AsyncHandlerType ||
                                  type.GetGenericTypeDefinition() == AsyncResultHandlerType);

    private static IServiceCollection HandleNonCustomizedRegistration(this IServiceCollection serviceCollection, IEnumerable<Type> types, Type handlerServiceType, ResultCommanderConfiguration config)
    {
        foreach (var type in types)
        {
            var closedGenericTypes = type.GetInterfaces().Where(IsHandlerInterface).ToList();

            switch (config.DefaultHandlerLifetime)
            {
                case ServiceLifetime.SingleInstance:
                    closedGenericTypes.ForEach(x => serviceCollection.AddSingleton(x, type));
                    break;
                case ServiceLifetime.InstancePerRequest:
                    closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, type));
                    break;
                case ServiceLifetime.InstancePerLifetimeScope:
                    closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, type));
                    break;
                case ServiceLifetime.InstancePerMatchingLifetimeScope:
                    throw new NotSupportedException("Supported only when using Autofac.");
                case ServiceLifetime.InstancePerOwned:
                    throw new NotSupportedException("Supported only when using Autofac.");
                case ServiceLifetime.InstancePerDependency:
                    closedGenericTypes.ForEach(x => serviceCollection.AddTransient(x, type));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return serviceCollection;
    }
    
    private static IServiceCollection HandleCustomizedRegistration(this IServiceCollection serviceCollection, IEnumerable<Type> types, Type handlerServiceType, ResultCommanderConfiguration config)
    {
        foreach (var type in types)
        {
            var lifeAttrs = type.GetRegistrationAttributesOfType<ILifetimeAttribute>().ToArray();
            if (lifeAttrs.Length > 1)
                throw new InvalidOperationException(
                    $"Only a single lifetime attribute is allowed on a type, {type.Name}");

            var lifeAttr = lifeAttrs.FirstOrDefault();

            var closedGenericTypes = type.GetInterfaces().Where(IsHandlerInterface).ToList();

            var lifetime = lifeAttr?.ServiceLifetime ?? config.DefaultHandlerLifetime;

            switch (lifetime)
            {
                case ServiceLifetime.SingleInstance:
                    closedGenericTypes.ForEach(x => serviceCollection.AddSingleton(x, type));
                    break;
                case ServiceLifetime.InstancePerRequest:
                    closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, type));
                    break;
                case ServiceLifetime.InstancePerLifetimeScope:
                    closedGenericTypes.ForEach(x => serviceCollection.AddScoped(x, type));;
                    break;
                case ServiceLifetime.InstancePerDependency:
                    closedGenericTypes.ForEach(x => serviceCollection.AddTransient(x, type));
                    break;
                case ServiceLifetime.InstancePerMatchingLifetimeScope:
                    throw new NotSupportedException("Supported only when using Autofac.");
                case ServiceLifetime.InstancePerOwned:
                    throw new NotSupportedException("Supported only when using Autofac.");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return serviceCollection;
    }
    private static IServiceCollection HandleAsynchronousHandlers(this IServiceCollection collection,
        List<Type> types, ResultCommanderConfiguration config)
        => collection.HandleNonCustomizedRegistration(types.Where(IsAsyncNonCustomizedHandler).ToList(),
                AsyncHandlerType, config)
            .HandleNonCustomizedRegistration(types.Where(IsAsyncResultNonCustomizedHandler).ToList(),
                AsyncResultHandlerType, config)
            .HandleCustomizedRegistration(types.Where(IsAsyncCustomizedHandler).ToList(), AsyncHandlerType, config)
            .HandleCustomizedRegistration(types.Where(IsAsyncResultCustomizedHandler).ToList(), AsyncResultHandlerType,
                config);

    private static IServiceCollection HandleSynchronousHandlers(this IServiceCollection collection,
        List<Type> types, ResultCommanderConfiguration config)
        => collection.HandleNonCustomizedRegistration(types.Where(IsSyncNonCustomizedHandler).ToList(), SyncHandlerType, config)
            .HandleNonCustomizedRegistration(types.Where(IsSyncResultNonCustomizedHandler).ToList(), SyncResultHandlerType, config)
            .HandleCustomizedRegistration(types.Where(IsSyncCustomizedHandler).ToList(), SyncHandlerType, config)
            .HandleCustomizedRegistration(types.Where(IsSyncResultCustomizedHandler).ToList(), SyncResultHandlerType,
                config);

    /// <summary>
    /// Registers command handlers with the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">Current instance of <see cref="IServiceCollection"/>.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for handlers.</param>
    /// <param name="options">Optional <see cref="ResultCommanderConfiguration"/> configuration.</param>
    /// <returns>Current <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddResultCommander(this IServiceCollection serviceCollection,
        IEnumerable<Type> assembliesContainingTypesToScan, Action<ResultCommanderConfiguration>? options = null)
        => AddResultCommander(serviceCollection, assembliesContainingTypesToScan.Select(x => x.Assembly).Distinct(), options);
        
    /// <summary>
    /// Registers command handlers with the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">Current instance of <see cref="IServiceCollection"/>.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for handlers.</param>
    /// <returns>Current <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddResultCommander(this IServiceCollection serviceCollection, params Type[] assembliesContainingTypesToScan)
        => AddResultCommander(serviceCollection, assembliesContainingTypesToScan, null);
    
    /// <summary>
    /// Registers command handlers with the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">Current service collection instance.</param>
    /// <param name="options">Configuration.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for services.</param>
    /// <returns>Current <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddResultCommander(this IServiceCollection serviceCollection, Action<ResultCommanderConfiguration> options, params Type[] assembliesContainingTypesToScan)
        => AddResultCommander(serviceCollection, assembliesContainingTypesToScan, options);
    
    /// <summary>
    /// Registers command handlers with the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">Current service collection instance.</param>
    /// <param name="options">Configuration.</param>
    /// <param name="assembliesToScan">Assemblies to scan for services.</param>
    /// <returns>Current <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddResultCommander(this IServiceCollection serviceCollection, Action<ResultCommanderConfiguration> options, params Assembly[] assembliesToScan)
        => AddResultCommander(serviceCollection, assembliesToScan, options);
    
    /// <summary>
    /// Registers command handlers with the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">Current instance of <see cref="IServiceCollection"/>.</param>
    /// <param name="assembliesToScan">Assemblies to scan for handlers.</param>
    /// <returns>Current <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddResultCommander(this IServiceCollection serviceCollection, params Assembly[] assembliesToScan)
        => AddResultCommander(serviceCollection, assembliesToScan, null);
    
    /// <summary>
    /// Registers command handlers with the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">Current instance of <see cref="IServiceCollection"/>.</param>
    /// <param name="assembliesToScaAn">Assemblies to scan for handlers.</param>
    /// <param name="options">Optional <see cref="ResultCommanderConfiguration"/> configuration.</param>
    /// <returns>Current <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddResultCommander(this IServiceCollection serviceCollection, IEnumerable<Assembly> assembliesToScaAn, Action<ResultCommanderConfiguration>? options = null)
    {
        var config = new ResultCommanderConfiguration();
        options?.Invoke(config);

        Action<ResultCommanderConfiguration> fallback = _ => { };

        serviceCollection.AddOptions<ResultCommanderConfiguration>().Configure(options ?? fallback);

        foreach (var assembly in assembliesToScaAn)
        {
            var types = assembly.GetTypes();
            serviceCollection.HandleAsynchronousHandlers(types.Where(IsAsyncHandler).ToList(), config);
            serviceCollection.HandleSynchronousHandlers(types.Where(IsSyncHandler).ToList(), config);
        }
        
        serviceCollection.RegisterByLifetime(typeof(ICommandHandlerResolver), typeof(CommandHandlerResolver), config.DefaultHandlerResolverLifetime);
        
        return serviceCollection;
    }

    private static IServiceCollection RegisterByLifetime(this IServiceCollection serviceCollection, Type serviceType, Type implementationType,
        ServiceLifetime serviceLifetime)
    {
        switch (serviceLifetime)
        {
            case ServiceLifetime.SingleInstance:
                serviceCollection.AddSingleton(serviceType, implementationType);
                break;
            case ServiceLifetime.InstancePerRequest:
                serviceCollection.AddScoped(serviceType, implementationType);
                break;
            case ServiceLifetime.InstancePerLifetimeScope:
                serviceCollection.AddScoped(serviceType, implementationType);
                break;
            case ServiceLifetime.InstancePerMatchingLifetimeScope:
                throw new NotSupportedException(
                    "Not supported without Autofac");
            case ServiceLifetime.InstancePerDependency:
                serviceCollection.AddTransient(serviceType, implementationType);
                break;
            case ServiceLifetime.InstancePerOwned:
                throw new NotSupportedException(
                    "Not supported without Autofac");
            default:
                throw new ArgumentOutOfRangeException();
        }

        return serviceCollection;
    }
}
