using Autofac;
using Quartz;

namespace CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Quartz
{
    // Autofac module that registers the Quartz jobs so the scheduler can create them through the container.
    public class QuartzModule : Autofac.Module
    {
        // Registers every IJob implementation of this assembly, creating a new instance per run.
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(ThisAssembly)
                .Where(x => typeof(IJob).IsAssignableFrom(x)).InstancePerDependency();
        }
    }
}