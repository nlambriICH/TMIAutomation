using System;
using System.Threading.Tasks;

namespace TMIAutomation
{
    public interface IStructure
    {
        Task CreateAsync(IProgress<double> progress, IProgress<string> message);
    }
}
