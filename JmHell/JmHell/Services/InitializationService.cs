using System.Threading.Tasks;

namespace JmHell.Services
{
    public class InitializationService
    {
        private readonly TaskCompletionSource<bool> _initializationTcs = new TaskCompletionSource<bool>();

        public Task InitializationTask => _initializationTcs.Task;

        public void CompleteInitialization()
        {
            _initializationTcs.TrySetResult(true);
        }
    }
}

