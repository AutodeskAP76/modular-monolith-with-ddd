using System.Reflection;
using Autofac;
using Autofac.Core;
using Autofac.Features.Variance;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure;
using CompanyName.MyMeetings.Modules.Meetings.Application.Configuration.Commands;
using FluentValidation;
using MediatR;
using MediatR.Pipeline;

namespace CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Mediation
{
    // Autofac module that wires MediatR so commands, queries and notifications reach their handlers.
    // In simple terms: MediatR is a post office that delivers each message (command, query, event) to the
    // class that handles it. This module builds the "address book" the post office uses, by telling the
    // dependency injection container which handler classes exist. It runs once, at module startup.
    public class MediatorModule : Autofac.Module
    {
        // Registers MediatR, the handler and validator types found in the module and the pre/post processor behaviors.
        protected override void Load(ContainerBuilder builder)
        {
            // MediatR asks for an IServiceProvider to look up handlers; supply our wrapper around Autofac,
            // unless someone else already registered one.
            builder.RegisterType<ServiceProviderWrapper>()
            .As<IServiceProvider>()
            .InstancePerDependency()
            .IfNotRegistered(typeof(IServiceProvider));

            // Register MediatR's own classes (including IMediator) so they can be injected where needed.
            builder.RegisterAssemblyTypes(typeof(IMediator).GetTypeInfo().Assembly)
                .AsImplementedInterfaces()
                .InstancePerLifetimeScope();

            // The kinds of classes the "address book" must know about: anything that handles a command,
            // request, event (notification), validates a request, or runs before/after/on error of a request.
            var mediatorOpenTypes = new[]
            {
                typeof(IRequestHandler<,>),
                typeof(INotificationHandler<>),
                typeof(IValidator<>),
                typeof(IRequestPreProcessor<>),
                typeof(IRequestHandler<>),
                typeof(IStreamRequestHandler<,>),
                typeof(IRequestPostProcessor<,>),
                typeof(IRequestExceptionHandler<,,>),
                typeof(IRequestExceptionAction<,>),
                typeof(ICommandHandler<>),
                typeof(ICommandHandler<,>),
            };

            // Let a handler written for a general type also receive more specific ones
            // (e.g. a handler of a base event gets its derived events), limited to the types above.
            builder.RegisterSource(new ScopedContravariantRegistrationSource(
                mediatorOpenTypes));

            // Scan the Application assembly (where handlers live) and this Infrastructure assembly,
            // and register every class that implements one of the handler kinds above.
            foreach (var mediatorOpenType in mediatorOpenTypes)
            {
                builder
                    .RegisterAssemblyTypes(Assemblies.Application, ThisAssembly)
                    .AsClosedTypesOf(mediatorOpenType)
                    .AsImplementedInterfaces()

                    // Handlers may have non-public constructors; allow the container to use them.
                    .FindConstructorsWith(new AllConstructorFinder());
            }

            // Plug in the steps of the MediatR pipeline that run code before and after every handler.
            builder.RegisterGeneric(typeof(RequestPostProcessorBehavior<,>)).As(typeof(IPipelineBehavior<,>));
            builder.RegisterGeneric(typeof(RequestPreProcessorBehavior<,>)).As(typeof(IPipelineBehavior<,>));
        }

        // Registration source that enables contravariant resolution only for the given MediatR handler types.
        // Autofac's built-in version would apply this "general handler accepts specific messages" rule to
        // everything in the container; this wrapper restricts it to the MediatR handler types only.
        private class ScopedContravariantRegistrationSource : IRegistrationSource
        {
            private readonly ContravariantRegistrationSource _source = new();
            private readonly List<Type> _types = new();

            // Validates and stores the generic handler types the source applies to.
            public ScopedContravariantRegistrationSource(params Type[] types)
            {
                ArgumentNullException.ThrowIfNull(types);

                if (!types.All(x => x.IsGenericTypeDefinition))
                {
                    throw new ArgumentException("Supplied types should be generic type definitions");
                }

                _types.AddRange(types);
            }

            // Returns the contravariant registrations whose services match one of the stored handler types.
            public IEnumerable<IComponentRegistration> RegistrationsFor(
                Service service,
                Func<Service, IEnumerable<ServiceRegistration>> registrationAccessor)
            {
                // Ask the standard source for its candidates, then keep only the ones we allow.
                var components = _source.RegistrationsFor(service, registrationAccessor);
                foreach (var c in components)
                {
                    // Which generic handler kind (e.g. INotificationHandler<>) does this candidate implement?
                    var defs = c.Target.Services
                        .OfType<TypedService>()
                        .Select(x => x.ServiceType.GetGenericTypeDefinition());

                    if (defs.Any(_types.Contains))
                    {
                        yield return c;
                    }
                }
            }

            // Delegates to the wrapped source to tell Autofac whether this source adapts individual components.
            public bool IsAdapterForIndividualComponents => _source.IsAdapterForIndividualComponents;
        }
    }
}