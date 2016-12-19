﻿using System;
using System.Threading;
using System.Threading.Tasks;

using ProGaudi.Tarantool.Client.Model;
using ProGaudi.Tarantool.Client.Model.Requests;
using ProGaudi.Tarantool.Client.Model.Responses;
using ProGaudi.Tarantool.Client.Utils;

namespace ProGaudi.Tarantool.Client
{
    public class LogicalConnectionManager : ILogicalConnection
    {
        private readonly ClientOptions _clientOptions;

        private readonly RequestIdCounter _requestIdCounter = new RequestIdCounter();

        private LogicalConnection _droppableLogicalConnection;

        private readonly ManualResetEvent _connected = new ManualResetEvent(false);

        private readonly AutoResetEvent _reconnectAvailable = new AutoResetEvent(true);

        private Timer _timer;

        private int _disposing;

        private const int connectionTimeout = 1000;

        private const int connectionPingInterval = 1000;

        public LogicalConnectionManager(ClientOptions options)
        {
            _clientOptions = options;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposing, 1) > 0)
            {
                return;
            }

            Interlocked.Exchange(ref _droppableLogicalConnection, null)?.Dispose();

            var savedTimer = Interlocked.Exchange(ref _timer, null);
            if (savedTimer != null)
            {
                using (var timerDisposedEvent = new ManualResetEvent(false))
                {
                    savedTimer.Dispose(timerDisposedEvent);
                    // this will guarantee that all callbacks finished
                    timerDisposedEvent.WaitOne();
                }
            }
        }

        public async Task Connect()
        {
            _clientOptions.LogWriter?.WriteLine($"{nameof(LogicalConnectionManager)}: Connecting...");

            _connected.Reset();

            var _newConnection = new LogicalConnection(_clientOptions, _requestIdCounter);
            await _newConnection.Connect();
            Interlocked.Exchange(ref _droppableLogicalConnection, _newConnection)?.Dispose();

            _connected.Set();

            _clientOptions.LogWriter?.WriteLine($"{nameof(LogicalConnectionManager)}: Connected...");

            _timer = new Timer(x => CheckPing(), null, connectionPingInterval, Timeout.Infinite);
        }

        private static readonly PingRequest pingRequest = new PingRequest();

        private void CheckPing()
        {
            LogicalConnection savedConnection = _droppableLogicalConnection;

            if (savedConnection == null)
            {
                return;
            }

            try
            {
                Task task = savedConnection.SendRequestWithEmptyResponse(pingRequest);
                if (!task.Wait(connectionTimeout) || task.Status != TaskStatus.RanToCompletion)
                {
                    savedConnection.Dispose();
                }
            }
            catch (AggregateException ae)
            {
                savedConnection.Dispose();
            }
            finally
            {
                if (_disposing == 0)
                {
                    _timer?.Change(connectionPingInterval, Timeout.Infinite);
                }
            }
        }

        public bool IsConnected()
        {
            return _droppableLogicalConnection?.IsConnected() ?? false;
        }

        private async Task EnsureConnection()
        {
            if (_connected.WaitOne(connectionTimeout) && IsConnected())
            {
                return;
            }

            _clientOptions.LogWriter?.WriteLine($"{nameof(LogicalConnectionManager)}: Connection lost, wait for reconnect...");

            if (!_reconnectAvailable.WaitOne(connectionTimeout))
            {
                _clientOptions.LogWriter?.WriteLine($"{nameof(LogicalConnectionManager)}: Failed to get lock for reconnect");
                throw ExceptionHelper.NotConnected();
            }

            try
            {
                if (!IsConnected())
                {
                    await Connect();
                }

                _clientOptions.LogWriter?.WriteLine($"{nameof(LogicalConnectionManager)}: Connection reacquired");
            }
            finally
            {
                _reconnectAvailable.Set();
            }
        }

        public async Task<DataResponse<TResponse[]>> SendRequest<TRequest, TResponse>(TRequest request) where TRequest : IRequest
        {
            await EnsureConnection();
            return await _droppableLogicalConnection.SendRequest<TRequest, TResponse>(request);
        }

        public async Task SendRequestWithEmptyResponse<TRequest>(TRequest request) where TRequest : IRequest
        {
            await EnsureConnection();
            await _droppableLogicalConnection.SendRequestWithEmptyResponse(request);
        }
    }
}