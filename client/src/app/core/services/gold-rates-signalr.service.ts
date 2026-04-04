import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, HttpTransportType, LogLevel } from '@microsoft/signalr';
import { AuthService } from './auth.service';
import { DashboardRefreshService } from './dashboard-refresh.service';

@Injectable({ providedIn: 'root' })
export class GoldRatesSignalRService {
  private auth = inject(AuthService);
  private refresh = inject(DashboardRefreshService);

  private hub?: HubConnection;

  start(): void {
    const token = this.auth.getToken();
    if (!token)
      return;

    if (this.hub?.state === HubConnectionState.Connected)
      return;

    void this.hub?.stop();

    const url = `${window.location.origin}/hubs/gold-rates`;
    this.hub = new HubConnectionBuilder()
      .withUrl(url, {
        accessTokenFactory: () => this.auth.getToken() ?? '',
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.hub.on('RatesUpdated', () => this.refresh.triggerRefresh());

    this.hub.start().catch(() => {
      /* Sunucu kapalı veya proxy: panel yine de çalışır */
    });
  }

  stop(): void {
    if (!this.hub)
      return;
    void this.hub.stop();
    this.hub = undefined;
  }
}
