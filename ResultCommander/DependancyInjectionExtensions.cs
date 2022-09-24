﻿using System.Reflection;
using AttributeBasedRegistration;
using AttributeBasedRegistration.Attributes;
using Autofac;
using Autofac.Builder;
using Autofac.Extras.DynamicProxy;
using Autofac.Features.Scanning;
using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceLifetime = AttributeBasedRegistration.ServiceLifetime;

namespace ResultCommander;

/// <summary>
/// DI extensions for <see cref="ContainerBuilder"/>.
/// </summary>
[PublicAPI]
public static class DependancyInjectionExtensions
{
    private static Type _asyncResultHandlerType = typeof(IAsyncCommandHandler<,>);
    private static Type _asyncHandlerType = typeof(IAsyncCommandHandler<>);
    private static Type _syncResultHandlerType = typeof(ISyncCommandHandler<,>);
    private static Type _syncHandlerType = typeof(ISyncCommandHandler<>);
    
    private static bool HasCustomAttributes(this Type type)
        => type.GetCustomAttribute<LifetimeAttribute>(false) is not null ||
           type.GetCustomAttributes<DecoratedByAttribute>(false).Any() ||
           type.GetCustomAttributes<InterceptedByAttribute>(false).Any();

    private static bool ShouldIgnore(this Type type)
        => type.GetCustomAttribute<SkipHandlerRegistrationAttribute>(false) is not null;
    
    private static bool IsAnyAsyncHandler(this Type type)
            => (type.IsAsyncHandler() || type.IsAsyncResultHandler()) && !ShouldIgnore(type);
    
    private static bool IsAsyncHandler(this Type type)
        => type.GetInterfaces().Any(y =>
               y.IsGenericType && y.GetGenericTypeDefinition() == _asyncHandlerType) &&
           type.IsClass && !type.IsAbstract && !ShouldIgnore(type);
    
    private static bool IsAsyncResultHandler(this Type type)
        => type.GetInterfaces().Any(y =>
               y.IsGenericType && y.GetGenericTypeDefinition() == _asyncResultHandlerType) &&
           type.IsClass && !type.IsAbstract && !ShouldIgnore(type);

    private static bool IsAsyncResultCustomizedHandler(this Type type)
        => IsAsyncResultHandler(type) && HasCustomAttributes(type);
    
    private static bool IsAsyncCustomizedHandler(this Type type)
        => IsAsyncHandler(type) && HasCustomAttributes(type);
    
    private static bool IsAsyncResultNonCustomizedHandler(this Type type)
        => IsAsyncResultHandler(type) && !HasCustomAttributes(type);
    
    private static bool IsAsyncNonCustomizedHandler(this Type type)
        => IsAsyncHandler(type) && !HasCustomAttributes(type);
    
    private static bool IsAnySyncHandler(this Type type)
        => (type.IsSyncHandler() || type.IsSyncResultHandler()) && !ShouldIgnore(type);
    
    private static bool IsSyncHandler(this Type type)
        => type.GetInterfaces().Any(y =>
               y.IsGenericType && y.GetGenericTypeDefinition() == _syncHandlerType) &&
           type.IsClass && !type.IsAbstract && !ShouldIgnore(type);
    
    private static bool IsSyncResultHandler(this Type type)
        => type.GetInterfaces().Any(y =>
               y.IsGenericType && y.GetGenericTypeDefinition() == _syncResultHandlerType) &&
           type.IsClass && !type.IsAbstract && !ShouldIgnore(type);

    private static bool IsSyncResultCustomizedHandler(this Type type)
        => IsSyncResultHandler(type) && HasCustomAttributes(type);
    
    private static bool IsSyncCustomizedHandler(this Type type)
        => IsSyncHandler(type) && HasCustomAttributes(type);
    
    private static bool IsSyncResultNonCustomizedHandler(this Type type)
        => IsSyncResultHandler(type) && !HasCustomAttributes(type);
    
    private static bool IsSyncNonCustomizedHandler(this Type type)
        => IsSyncHandler(type) && !HasCustomAttributes(type);

    private static bool IsHandlerInterface(this Type type)
        => type.IsInterface && type.IsGenericType && (type.GetGenericTypeDefinition() == _syncHandlerType ||
                                  type.GetGenericTypeDefinition() == _syncResultHandlerType ||
                                  type.GetGenericTypeDefinition() == _asyncHandlerType ||
                                  type.GetGenericTypeDefinition() == _asyncResultHandlerType);

    private static IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle> HandleInterception(this IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle> builder, Type type)
    {
        var intrAttr = type.GetCustomAttribute<EnableInterceptionAttribute>(false);
        if (intrAttr is null)
            return builder;

        if (intrAttr.InterceptionStrategy is not (InterceptionStrategy.Interface))
            throw new NotSupportedException("Only interface interception is supported for command handlers");
                
        var intrAttrs = type.GetCustomAttributes<InterceptedByAttribute>(false);

        builder = builder.EnableInterfaceInterceptors();
        
        foreach (var interceptor in intrAttrs.OrderByDescending(x => x.RegistrationOrder).Select(x => x.Interceptor).Distinct())
        {
            builder = interceptor.IsAsyncInterceptor()
                ? builder.InterceptedBy(
                    typeof(AsyncInterceptorAdapter<>).MakeGenericType(interceptor))
                : builder.InterceptedBy(interceptor);
        }

        return builder;
    }
    
    private static ContainerBuilder HandleDecoration(this ContainerBuilder builder, Type type)
    {
        var decAttrs = type.GetCustomAttributes<DecoratedByAttribute>(false).ToList();
        if (!decAttrs.Any())
            return builder;

        var serviceType = type.GetInterfaces().FirstOrDefault(x => x.IsHandlerInterface());

        if (serviceType is null)
            throw new InvalidOperationException("Couldn't fine the proper service type for a handler");

        foreach (var decAttr in decAttrs.OrderBy(x => x.RegistrationOrder))
        {
            if (serviceType.IsGenericType && serviceType.IsGenericTypeDefinition)
                throw new InvalidOperationException(
                    "Can't register an non-open generic type decorator for an open generic type service");
                            
            builder.RegisterDecorator(decAttr.DecoratorType, serviceType);
        }

        return builder;
    }
    
    private static IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle> HandleLifetime(this IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle> builder, ServiceLifetime lifetime, LifetimeAttribute? attribute)
    {
        switch (lifetime)
        {
            case ServiceLifetime.SingleInstance:
                builder = builder.SingleInstance();
                break;
            case ServiceLifetime.InstancePerRequest:
                builder = builder.InstancePerRequest();
                break;
            case ServiceLifetime.InstancePerLifetimeScope:
                builder = builder.InstancePerLifetimeScope();
                break;
            case ServiceLifetime.InstancePerDependency:
                builder = builder.InstancePerDependency();
                break;
            case ServiceLifetime.InstancePerMatchingLifetimeScope:
                builder =
                    builder.InstancePerMatchingLifetimeScope(attribute?.Tags?.ToArray() ?? throw new InvalidOperationException());
                break;
            case ServiceLifetime.InstancePerOwned:
                if (attribute?.Owned is null) throw new InvalidOperationException("Owned type was null");

                builder = builder.InstancePerOwned(attribute.Owned);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }   
        
        return builder;
    }

    private static ContainerBuilder HandleNonCustomizedRegistration(this ContainerBuilder builder,
        List<Type> types, Type handlerServiceType, ResultCommanderConfiguration config)
    {
        if (!types.Any())
            return builder;
        
        switch (config.DefaultHandlerLifetime)
        {
            case ServiceLifetime.SingleInstance:
                builder.RegisterTypes(types.ToArray())
                    .AsClosedInterfacesOf(handlerServiceType).SingleInstance();
                break;
            case ServiceLifetime.InstancePerRequest:
                builder.RegisterTypes(types.ToArray())
                    .AsClosedInterfacesOf(handlerServiceType).InstancePerRequest();
                break;
            case ServiceLifetime.InstancePerLifetimeScope:
                builder.RegisterTypes(types.ToArray())
                    .AsClosedInterfacesOf(handlerServiceType).InstancePerLifetimeScope();
                break;
            case ServiceLifetime.InstancePerMatchingLifetimeScope:
                throw new NotSupportedException();
            case ServiceLifetime.InstancePerDependency:
                builder.RegisterTypes(types.ToArray())
                    .AsClosedInterfacesOf(handlerServiceType).InstancePerDependency();
                break;
            case ServiceLifetime.InstancePerOwned:
                throw new NotSupportedException();
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        return builder;
    }
    
    private static ContainerBuilder HandleCustomizedRegistration(this ContainerBuilder builder, IEnumerable<Type> types, Type handlerServiceType, ResultCommanderConfiguration config)
    {
        foreach (var type in types)
        {
            var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

            var registrationBuilder = builder.RegisterTypes(type).AsClosedInterfacesOf(handlerServiceType);

            var lifetime = lifeAttr?.ServiceLifetime ?? config.DefaultHandlerLifetime;

            registrationBuilder.HandleLifetime(lifetime, lifeAttr);

            registrationBuilder.HandleInterception(type);
            
            builder.HandleDecoration(type);
        }

        return builder;
    }
    
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
            var lifeAttr = type.GetCustomAttribute<LifetimeAttribute>(false);

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

    private static ContainerBuilder HandleAsynchronousHandlers(this ContainerBuilder builder,
        List<Type> types, ResultCommanderConfiguration config)
        => builder.HandleNonCustomizedRegistration(types.Where(IsAsyncNonCustomizedHandler).ToList(), _asyncHandlerType,
                config)
            .HandleNonCustomizedRegistration(types.Where(IsAsyncResultNonCustomizedHandler).ToList(),
                _asyncResultHandlerType, config)
            .HandleCustomizedRegistration(types.Where(IsAsyncCustomizedHandler), _asyncHandlerType, config)
            .HandleCustomizedRegistration(types.Where(IsAsyncResultCustomizedHandler), _asyncResultHandlerType, config);

    private static ContainerBuilder HandleSynchronousHandlers(this ContainerBuilder builder,
        List<Type> types, ResultCommanderConfiguration config)
        => builder.HandleNonCustomizedRegistration(types.Where(IsSyncNonCustomizedHandler).ToList(), _syncHandlerType,
                config)
            .HandleNonCustomizedRegistration(types.Where(IsSyncResultNonCustomizedHandler).ToList(),
                _syncResultHandlerType, config)
            .HandleCustomizedRegistration(types.Where(IsSyncCustomizedHandler), _syncHandlerType, config)
            .HandleCustomizedRegistration(types.Where(IsSyncResultCustomizedHandler), _syncResultHandlerType, config);

    private static IServiceCollection HandleAsynchronousHandlers(this IServiceCollection collection,
        List<Type> types, ResultCommanderConfiguration config)
        => collection.HandleNonCustomizedRegistration(types.Where(IsAsyncNonCustomizedHandler).ToList(),
                _asyncHandlerType, config)
            .HandleNonCustomizedRegistration(types.Where(IsAsyncResultNonCustomizedHandler).ToList(),
                _asyncResultHandlerType, config)
            .HandleCustomizedRegistration(types.Where(IsAsyncCustomizedHandler).ToList(), _asyncHandlerType, config)
            .HandleCustomizedRegistration(types.Where(IsAsyncResultCustomizedHandler).ToList(), _asyncResultHandlerType,
                config);

    private static IServiceCollection HandleSynchronousHandlers(this IServiceCollection collection,
        List<Type> types, ResultCommanderConfiguration config)
        => collection.HandleNonCustomizedRegistration(types.Where(IsSyncNonCustomizedHandler).ToList(), _syncHandlerType, config)
            .HandleNonCustomizedRegistration(types.Where(IsSyncResultNonCustomizedHandler).ToList(), _syncResultHandlerType, config)
            .HandleCustomizedRegistration(types.Where(IsSyncCustomizedHandler).ToList(), _syncHandlerType, config)
            .HandleCustomizedRegistration(types.Where(IsSyncResultCustomizedHandler).ToList(), _syncResultHandlerType,
                config);

    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for handlers.</param>
    /// <param name="options">Optional <see cref="ResultCommanderConfiguration"/> configuration.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, IEnumerable<Type> assembliesContainingTypesToScan, Action<ResultCommanderConfiguration>? options = null)
        => AddResultCommander(builder, assembliesContainingTypesToScan.Select(x => x.Assembly).Distinct(), options);

    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for handlers.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, params Type[] assembliesContainingTypesToScan)
        => AddResultCommander(builder, assembliesContainingTypesToScan, null);
    
    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="assembliesToScan">Assemblies to scan for handlers.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, params Assembly[] assembliesToScan)
        => AddResultCommander(builder, assembliesToScan, null);
    
    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="options">Configuration.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for services.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, Action<ResultCommanderConfiguration> options, params Type[] assembliesContainingTypesToScan)
        => AddResultCommander(builder, assembliesContainingTypesToScan, options);
    
    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="options">Configuration.</param>
    /// <param name="assembliesToScan">Assemblies to scan for services.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, Action<ResultCommanderConfiguration> options, params Assembly[] assembliesToScan)
        => AddResultCommander(builder, assembliesToScan, options);
    
    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="assembliesToScan">Assemblies to scan for handlers.</param>
    /// <param name="options">Optional <see cref="ResultCommanderConfiguration"/> configuration.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, IEnumerable<Assembly> assembliesToScan, Action<ResultCommanderConfiguration>? options = null)
    {
        var config = new ResultCommanderConfiguration(builder);
        options?.Invoke(config);

        var iopt = Options.Create(config);

        builder.RegisterInstance(iopt).As<IOptions<ResultCommanderConfiguration>>().SingleInstance();
        builder.Register(x => x.Resolve<IOptions<ResultCommanderConfiguration>>().Value)
            .As<ResultCommanderConfiguration>().SingleInstance();

        foreach (var assembly in assembliesToScan)
        {
            var types = assembly.GetTypes();
            builder.HandleAsynchronousHandlers(types.Where(IsAnyAsyncHandler).ToList(), config);
            builder.HandleSynchronousHandlers(types.Where(IsAnySyncHandler).ToList(), config);
        }

        return builder;
    }

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
    /// <param name="serviceCollection">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for handlers.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static IServiceCollection AddResultCommander(this IServiceCollection serviceCollection, params Type[] assembliesContainingTypesToScan)
        => AddResultCommander(serviceCollection, assembliesContainingTypesToScan, null);
    
    /// <summary>
    /// Registers command handlers with the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">Current service collection instance.</param>
    /// <param name="options">Configuration.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for services.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static IServiceCollection AddResultCommander(this IServiceCollection serviceCollection, Action<ResultCommanderConfiguration> options, params Type[] assembliesContainingTypesToScan)
        => AddResultCommander(serviceCollection, assembliesContainingTypesToScan, options);
    
    /// <summary>
    /// Registers command handlers with the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">Current service collection instance.</param>
    /// <param name="options">Configuration.</param>
    /// <param name="assembliesToScan">Assemblies to scan for services.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static IServiceCollection AddResultCommander(this IServiceCollection serviceCollection, Action<ResultCommanderConfiguration> options, params Assembly[] assembliesToScan)
        => AddResultCommander(serviceCollection, assembliesToScan, options);
    
    /// <summary>
    /// Registers command handlers with the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">Current instance of <see cref="IServiceCollection"/>.</param>
    /// <param name="assembliesToScan">Assemblies to scan for handlers.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
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
        var config = new ResultCommanderConfiguration(serviceCollection);
        options?.Invoke(config);

        var iopt = Options.Create(config);
        serviceCollection.AddSingleton(iopt);
        serviceCollection.AddSingleton(x =>
            x.GetRequiredService<IOptions<ResultCommanderConfiguration>>().Value);

        foreach (var assembly in assembliesToScaAn)
        {
            var types = assembly.GetTypes();
            serviceCollection.HandleAsynchronousHandlers(types.Where(IsAsyncHandler).ToList(), config);
            serviceCollection.HandleSynchronousHandlers(types.Where(IsSyncHandler).ToList(), config);
        }
        
        return serviceCollection;
    }
    
    /// <summary>
    /// Whether given interceptor is an async interceptor.
    /// </summary>
    private static bool IsAsyncInterceptor(this Type interceptorCandidate) => interceptorCandidate.GetInterfaces().Any(x => x == typeof(IAsyncInterceptor));
}
