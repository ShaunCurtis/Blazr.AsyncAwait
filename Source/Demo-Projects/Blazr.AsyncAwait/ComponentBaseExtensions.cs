using Blazr.Async;
using Microsoft.AspNetCore.Components;

namespace Blazr.AsyncAwait
{
    public static class ComponentBaseExtensions
    {
        /// <summary>
        /// Extension method to call StateHasChannged
        /// and force a render of the component
        /// </summary>
        /// <param name="component"></param>
        /// <param name="stateHasChanged"></param>
        /// <returns></returns>
        public static BlazrYield RenderAsync(this ComponentBase component, Action stateHasChanged)
        {

            var yielder = BlazrYield.Yield();
            yielder.OnCompleted(stateHasChanged);
            return yielder;

        }
    }
}
