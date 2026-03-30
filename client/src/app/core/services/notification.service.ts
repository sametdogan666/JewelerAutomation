import { Injectable } from '@angular/core';
import Swal from 'sweetalert2';

const DARK_THEME = {
  background: '#1e1e2f',
  color: '#e0e0e0',
  confirmButtonColor: '#7b2cbf',
  cancelButtonColor: '#6c757d',
};

@Injectable({ providedIn: 'root' })
export class NotificationService {

  success(title: string, text?: string): void {
    Swal.fire({
      icon: 'success',
      title,
      text,
      timer: 3000,
      timerProgressBar: true,
      showConfirmButton: false,
      toast: true,
      position: 'top-end',
      background: '#1b5e20',
      color: '#e8f5e9',
      iconColor: '#a5d6a7',
    });
  }

  error(title: string, text?: string): void {
    Swal.fire({
      icon: 'error',
      title,
      text,
      ...DARK_THEME,
      confirmButtonText: 'Tamam',
    });
  }

  async confirmDelete(itemDescription: string): Promise<boolean> {
    const result = await Swal.fire({
      icon: 'warning',
      title: 'Silme Onayı',
      html: `<span style="font-size:1rem"><b>${itemDescription}</b></span><br><span style="color:#aaa;font-size:.875rem">Bu işlem geri alınamaz.</span>`,
      showCancelButton: true,
      confirmButtonText: 'Evet, Sil',
      cancelButtonText: 'Vazgeç',
      ...DARK_THEME,
      confirmButtonColor: '#c62828',
      focusCancel: true,
      customClass: {
        popup: 'swal-premium-popup',
        title: 'swal-premium-title',
        confirmButton: 'swal-premium-confirm',
        cancelButton: 'swal-premium-cancel',
      },
    });
    return result.isConfirmed;
  }

  async confirm(title: string, text: string): Promise<boolean> {
    const result = await Swal.fire({
      icon: 'question',
      title,
      text,
      showCancelButton: true,
      confirmButtonText: 'Evet',
      cancelButtonText: 'Hayır',
      ...DARK_THEME,
    });
    return result.isConfirmed;
  }

  info(title: string, text?: string): void {
    Swal.fire({
      icon: 'info',
      title,
      text,
      ...DARK_THEME,
      confirmButtonText: 'Tamam',
    });
  }
}
