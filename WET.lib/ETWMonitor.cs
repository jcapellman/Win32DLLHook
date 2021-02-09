﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

using WET.lib.Enums;
using WET.lib.Extensions;
using WET.lib.MonitorItems;
using WET.lib.Monitors.Base;

namespace WET.lib
{
    public class ETWMonitor : IDisposable
    {
        public const string DefaultSessionName = nameof(ETWMonitor);

        private readonly CancellationTokenSource _ctSource = new();

        private TraceEventSession _session;

        public event EventHandler<FileReadMonitorItem> OnFileRead; 

        public event EventHandler<ImageLoadMonitorItem> OnImageLoad;

        public event EventHandler<ImageUnloadMonitorItem> OnImageUnload;

        public event EventHandler<ProcessStartMonitorItem> OnProcessStart;

        public event EventHandler<ProcessStopMonitorItem> OnProcessStop;

        public event EventHandler<RegistryUpdateMonitorItem> OnRegistryUpdate;

        private readonly List<BaseMonitor> _monitors;
        
        private object ParseData(TraceEvent eventData, MonitorTypes monitorType) =>
            _monitors.FirstOrDefault(a => a.MonitorType == monitorType)?.ParseTraceEvent(eventData);

        public ETWMonitor()
        {
            _monitors = GetType().Assembly.GetTypes().Where(a => a.BaseType == typeof(BaseMonitor))
                .Select(a => (BaseMonitor) Activator.CreateInstance(a)).ToList();
        }

        private void InitializeMonitor(string sessionName, MonitorTypes monitorTypes)
        {
            _session = new TraceEventSession(sessionName);

            var enabledMonitors = _monitors.Where(a => monitorTypes.HasFlag(a.MonitorType)).ToList();

            _session.EnableKernelProvider(enabledMonitors.Select(a => a.KeyWordMap).ToKeywords());

            foreach (var monitor in enabledMonitors)
            {
                switch (monitor.MonitorType)
                {
                    case MonitorTypes.FileRead:
                        _session.Source.Kernel.DiskIORead += Kernel_DiskIORead;
                        break;
                    case MonitorTypes.ImageLoad:
                        _session.Source.Kernel.ImageLoad += Kernel_ImageLoad;
                        break;
                    case MonitorTypes.ImageUnload:
                        _session.Source.Kernel.ImageUnload += Kernel_ImageUnload;
                        break;
                    case MonitorTypes.ProcessStart:
                        _session.Source.Kernel.ProcessStart += Kernel_ProcessStart;
                        break;
                    case MonitorTypes.ProcessStop:
                        _session.Source.Kernel.ProcessStop += Kernel_ProcessStop;
                        break;
                    case MonitorTypes.RegistryUpdate:
                        _session.Source.Kernel.RegistrySetValue += Kernel_RegistrySetValue;
                        break;
                }
            }

            _session.Source.Process();
        }

        private void Kernel_RegistrySetValue(Microsoft.Diagnostics.Tracing.Parsers.Kernel.RegistryTraceData obj) =>
            OnRegistryUpdate?.Invoke(this, (RegistryUpdateMonitorItem)ParseData(obj, MonitorTypes.RegistryUpdate));

        private void Kernel_DiskIORead(Microsoft.Diagnostics.Tracing.Parsers.Kernel.DiskIOTraceData obj) =>
            OnFileRead?.Invoke(this, (FileReadMonitorItem)ParseData(obj, MonitorTypes.FileRead));
        
        public void Start(string sessionName = DefaultSessionName, MonitorTypes monitorTypes = MonitorTypes.ImageLoad | MonitorTypes.ProcessStart)
        {
            Task.Run(() =>
            {
                InitializeMonitor(sessionName, monitorTypes);
            }, _ctSource.Token);
        }

        private void Kernel_ProcessStop(Microsoft.Diagnostics.Tracing.Parsers.Kernel.ProcessTraceData obj) =>
            OnProcessStop?.Invoke(this, (ProcessStopMonitorItem)ParseData(obj, MonitorTypes.ProcessStop));

        private void Kernel_ProcessStart(Microsoft.Diagnostics.Tracing.Parsers.Kernel.ProcessTraceData obj) => 
            OnProcessStart?.Invoke(this, (ProcessStartMonitorItem)ParseData(obj, MonitorTypes.ProcessStart));

        private void Kernel_ImageLoad(Microsoft.Diagnostics.Tracing.Parsers.Kernel.ImageLoadTraceData obj) =>
            OnImageLoad?.Invoke(this, (ImageLoadMonitorItem)ParseData(obj, MonitorTypes.ImageLoad));

        private void Kernel_ImageUnload(Microsoft.Diagnostics.Tracing.Parsers.Kernel.ImageLoadTraceData obj) =>
            OnImageUnload?.Invoke(this, (ImageUnloadMonitorItem)ParseData(obj, MonitorTypes.ImageUnload));

        public void Stop()
        {
            _ctSource.Cancel();

            _session.Stop(true);
        }
        
        public void Dispose()
        {
            _ctSource.Cancel();

            _session.Stop(true);

            GC.SuppressFinalize(this);
        }
    }
}