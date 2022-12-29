using AngryWasp.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace AngryWasp.Net
{
    public interface ITimedEvent
    {
        Task Execute();
    }

    public class TimedEventAttribute : Attribute
    {
        private int interval;

        public int Interval => interval;

        public TimedEventAttribute(int interval)
        {
            this.interval = interval * 1000;
        }
    }

    public static class TimedEventManager
    {
        private static Dictionary<string, TimedEvent> events = new Dictionary<string, TimedEvent>();

        public static void RegisterEvents(Assembly assembly)
        {
            var types = ReflectionHelper.Instance.GetTypesInheritingOrImplementing(assembly, typeof(ITimedEvent))
                .Where(m => m.GetCustomAttributes(typeof(TimedEventAttribute), false).Length > 0)
                .ToArray();

            foreach (var type in types)
            {
                ITimedEvent ia = (ITimedEvent)Activator.CreateInstance(type);
                TimedEventAttribute a = ia.GetType().GetCustomAttributes(true).OfType<TimedEventAttribute>().FirstOrDefault();
                TimedEventManager.RegisterEvent(type.Name, ia.Execute, a.Interval);
            }
        }

        public static void RegisterEvent(string name, Func<Task> action, int frequency) => events.Add(name, TimedEvent.Create(action, frequency));

        public static void Stop(string name) => events[name].Stop();

        public static void Start(string name) => events[name].Start();
    }
}