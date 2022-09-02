using System;
using System.Threading.Tasks;

namespace TMIJunction
{
    public interface IStructure
    {
        Task CreateAsync(IProgress<double> progress, IProgress<string> message);
    }
}
