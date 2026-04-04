import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { tap } from 'rxjs';

/**
 * Tüm başarısız HTTP yanıtlarında tam URL ve durumu konsola yazar (404 ayıklama).
 */
export const httpErrorLogInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    tap({
      error: (err: HttpErrorResponse) => {
        console.error('[HTTP failed]', {
          status: err.status,
          method: req.method,
          url: req.url,
          urlWithParams: req.urlWithParams,
          message: err.message,
        });
      },
    }),
  );
