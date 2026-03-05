import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatSidenavModule, MatDrawerMode } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { AuthService } from '../core/services/auth.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
  ],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss',
})
export class MainLayoutComponent {
  private breakpoint = inject(BreakpointObserver);
  private auth = inject(AuthService);

  drawerOpened = signal(true);
  drawerMode = signal<MatDrawerMode>('side');
  isHandset = signal(false);

  currentUser = this.auth.currentUser;

  navItems = [
    { path: '/dashboard', icon: 'dashboard', label: 'Panel' },
    { path: '/transactions', icon: 'swap_horiz', label: 'Alış-Satış' },
    { path: '/customers', icon: 'people', label: 'Cariler' },
    { path: '/kasa', icon: 'account_balance_wallet', label: 'Kasa' },
  ];

  constructor() {
    this.breakpoint.observe([Breakpoints.Handset]).subscribe((state) => {
      const handset = state.matches;
      this.isHandset.set(handset);
      this.drawerMode.set(handset ? 'over' : 'side');
      this.drawerOpened.set(!handset);
    });
  }

  toggleDrawer(): void {
    this.drawerOpened.update((v) => !v);
  }

  closeDrawerIfHandset(): void {
    if (this.isHandset()) this.drawerOpened.set(false);
  }

  logout(): void {
    this.auth.logout();
  }
}
