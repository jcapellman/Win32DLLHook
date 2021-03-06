﻿using System.Collections.Generic;
using System.Threading.Tasks;

using WET.lib.Containers;

namespace WET.lib.Interfaces
{
    public interface IEventStorage
    {
        public Task<bool> WriteEventAsync(ETWEventContainerItem item);

        public void Shutdown();

        public Task<bool> WriteBatchEventAsync(List<ETWEventContainerItem> batch);
    }
}