using System;
using System.Threading.Tasks;
using System.Timers;

namespace AngryWasp.Net
{
    public class TimedEvent
    {
        private Timer timer;

        public static TimedEvent Create(Func<Task> action, int frequency)
        {
            TimedEvent te = new TimedEvent();
            te.timer = new Timer(frequency * 1000);

            te.timer.Elapsed += (s, e) =>
            {
                Task.Run(async () =>
                {
                    await action.Invoke().ConfigureAwait(false);
                });
            };

            te.Start();

            return te;
        }

        public void Start() => timer.Start();

        public void Stop() => timer.Stop();
    }
}