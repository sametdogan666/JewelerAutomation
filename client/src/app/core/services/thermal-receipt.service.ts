import { Injectable } from '@angular/core';
import { jsPDF } from 'jspdf';
import html2canvas from 'html2canvas';
import { Transaction, TransactionItem } from './transactions.service';

const RECEIPT_WIDTH_PX = 302; // ~80 mm @ 96dpi
const SHOP_NAME = 'DOĞAN KUYUMCULUK';

@Injectable({ providedIn: 'root' })
export class ThermalReceiptService {
  /**
   * 80 mm genişlikte termal fiş PDF üretir; yeni sekmede açar, açılamazsa indirir.
   */
  async openReceipt(tx: Transaction): Promise<void> {
    await document.fonts.ready;
    try {
      await document.fonts.load('600 15px "Noto Sans"');
      await document.fonts.load('400 11px "Noto Sans"');
    } catch {
      /* ağ fontu yoksa yerel yazı tipi kullanılır */
    }
    const root = this.buildReceiptElement(tx);
    try {
      const canvas = await html2canvas(root, {
        scale: 2,
        useCORS: true,
        backgroundColor: '#ffffff',
        logging: false,
        width: RECEIPT_WIDTH_PX,
        windowWidth: RECEIPT_WIDTH_PX,
      });

      const imgData = canvas.toDataURL('image/jpeg', 0.93);
      const imgWidthMm = 80;
      const imgHeightMm = (imgWidthMm * canvas.height) / canvas.width;
      const marginMm = 2;
      const pageH = Math.max(imgHeightMm + marginMm * 2, 40);

      const doc = new jsPDF({
        unit: 'mm',
        format: [imgWidthMm, pageH],
        orientation: 'portrait',
      });

      doc.addImage(imgData, 'JPEG', 0, marginMm, imgWidthMm, imgHeightMm);

      const blob = doc.output('blob');
      const url = URL.createObjectURL(blob);
      const fileName = `fis-${this.shortDocRef(tx)}.pdf`;

      const opened = window.open(url, '_blank', 'noopener,noreferrer');
      if (!opened) {
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        a.rel = 'noopener';
        a.click();
      }

      setTimeout(() => URL.revokeObjectURL(url), 120_000);
    } finally {
      root.remove();
    }
  }

  private shortDocRef(tx: Transaction): string {
    return tx.id.replace(/-/g, '').slice(0, 10).toUpperCase();
  }

  private isNakitBaglama(tx: Transaction): boolean {
    return !!tx.correlationId && (!tx.items || tx.items.length === 0);
  }

  private formatMoney(n: number): string {
    return n.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  private formatGram(n: number): string {
    return n.toLocaleString('tr-TR', { minimumFractionDigits: 3, maximumFractionDigits: 3 });
  }

  private formatMilyem(n: number): string {
    return n.toLocaleString('tr-TR', { minimumFractionDigits: 3, maximumFractionDigits: 3 });
  }

  private formatDateTime(iso: string): string {
    return new Date(iso).toLocaleString('tr-TR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  private itemLabel(it: TransactionItem): string {
    const d = (it.description || '').trim();
    return d.length > 0 ? d : 'Kalem';
  }

  private directionTr(d: number): string {
    return d === 0 ? 'Satış' : 'Alış';
  }

  private buildReceiptElement(tx: Transaction): HTMLDivElement {
    const root = document.createElement('div');
    root.setAttribute('data-thermal-receipt', '1');
    root.style.cssText = [
      `position:fixed`,
      `left:-12000px`,
      `top:0`,
      `width:${RECEIPT_WIDTH_PX}px`,
      `padding:14px 12px 18px`,
      `background:#fff`,
      `color:#111`,
      `font-family:'Noto Sans','Inter',system-ui,sans-serif`,
      `font-size:11px`,
      `line-height:1.4`,
      `box-sizing:border-box`,
      `-webkit-font-smoothing:antialiased`,
    ].join(';');

    const header = document.createElement('div');
    header.style.cssText = 'text-align:center;border-bottom:1px dashed #333;padding-bottom:10px;margin-bottom:10px;';
    header.innerHTML = `
      <div style="font-size:15px;font-weight:700;letter-spacing:0.04em;margin-bottom:4px;">${SHOP_NAME}</div>
      <div style="font-size:10px;color:#444;">${this.formatDateTime(tx.transactionDate)}</div>
      <div style="font-size:9px;color:#555;margin-top:6px;font-family:ui-monospace,monospace;word-break:break-all;">Fiş No: ${tx.id}</div>
    `;
    root.appendChild(header);

    if (tx.customerName) {
      const c = document.createElement('div');
      c.style.cssText = 'font-size:10px;margin-bottom:8px;color:#333;';
      c.textContent = `Cari: ${tx.customerName}`;
      root.appendChild(c);
    }

    if (tx.description) {
      const d = document.createElement('div');
      d.style.cssText = 'font-size:9px;color:#666;margin-bottom:8px;font-style:italic;';
      d.textContent = `Not: ${tx.description}`;
      root.appendChild(d);
    }

    if (this.isNakitBaglama(tx)) {
      const peg = this.peggingLines(tx);
      const block = document.createElement('div');
      block.style.cssText = 'border:1px dashed #999;padding:8px;margin-bottom:10px;font-size:10px;';
      block.innerHTML = `
        <div style="font-weight:600;margin-bottom:6px;">Nakit bağlama</div>
        <div>Has (gr): <strong>${this.formatGram(peg.hasGram)}</strong></div>
        <div>Nakit (₺): <strong>${this.formatMoney(peg.cashTl)}</strong></div>
      `;
      root.appendChild(block);
    } else if (!tx.items || tx.items.length === 0) {
      const empty = document.createElement('div');
      empty.style.cssText = 'font-size:10px;color:#555;margin-bottom:10px;padding:8px;border:1px dashed #ccc;';
      empty.textContent = 'Bu kayıtta kalem detayı yok; özet toplamlar aşağıdadır.';
      root.appendChild(empty);
    } else {
      const table = document.createElement('table');
      table.style.cssText = 'width:100%;border-collapse:collapse;font-size:10px;margin-bottom:10px;';
      table.innerHTML = `
        <thead>
          <tr style="border-bottom:1px solid #222;">
            <th style="text-align:left;padding:4px 2px;font-weight:600;">Ürün</th>
            <th style="text-align:right;padding:4px 2px;font-weight:600;width:44px;">Gr</th>
            <th style="text-align:right;padding:4px 2px;font-weight:600;width:48px;">Mil.</th>
            <th style="text-align:right;padding:4px 2px;font-weight:600;width:56px;">₺</th>
          </tr>
        </thead>
        <tbody></tbody>
      `;
      const tbody = table.querySelector('tbody')!;

      for (const it of tx.items || []) {
        const tr = document.createElement('tr');
        tr.style.cssText = 'border-bottom:1px dotted #ccc;vertical-align:top;';
        const totalTl = it.price ?? 0;
        tr.innerHTML = `
          <td style="padding:6px 2px;">
            <div style="font-weight:500;">${this.escapeHtml(this.itemLabel(it))}</div>
            <div style="font-size:8px;color:#666;">${this.directionTr(it.direction)}</div>
          </td>
          <td style="padding:6px 2px;text-align:right;white-space:nowrap;">${this.formatGram(it.quantity)}</td>
          <td style="padding:6px 2px;text-align:right;white-space:nowrap;">${this.formatMilyem(it.milyem)}</td>
          <td style="padding:6px 2px;text-align:right;white-space:nowrap;font-weight:600;">${this.formatMoney(totalTl)}</td>
        `;
        tbody.appendChild(tr);
      }
      root.appendChild(table);
    }

    const totals = document.createElement('div');
    totals.style.cssText =
      'border-top:2px solid #111;padding-top:10px;margin-top:4px;font-size:11px;';
    const netHas = tx.netHasGram;
    const netCash = tx.netCashAmount;
    totals.innerHTML = `
      <div style="display:flex;justify-content:space-between;margin-bottom:4px;">
        <span>Toplam Has (net)</span>
        <strong>${this.formatGram(netHas)} gr</strong>
      </div>
      <div style="display:flex;justify-content:space-between;margin-bottom:2px;">
        <span>Toplam Nakit (net)</span>
        <strong>${this.formatMoney(netCash)} ₺</strong>
      </div>
    `;
    root.appendChild(totals);

    const foot = document.createElement('div');
    foot.style.cssText =
      'text-align:center;margin-top:16px;padding-top:12px;border-top:1px dashed #999;font-size:10px;color:#444;';
    foot.innerHTML = `
      <div style="margin-bottom:14px;">Bizi tercih ettiğiniz için teşekkür ederiz.</div>
      <div style="font-size:9px;color:#888;margin-bottom:4px;">İmza</div>
      <div style="border-bottom:1px solid #333;height:28px;margin:0 8px;"></div>
    `;
    root.appendChild(foot);

    document.body.appendChild(root);
    return root;
  }

  private peggingLines(tx: Transaction): { hasGram: number; cashTl: number } {
    const fromCash =
      tx.cashAmount != null && tx.cashAmount !== undefined ? Math.abs(Number(tx.cashAmount)) : null;
    const cash =
      fromCash != null && !Number.isNaN(fromCash) && fromCash > 1e-9
        ? fromCash
        : Math.abs(Number(tx.netCashAmount ?? tx.price ?? 0));

    const fromEq =
      tx.equivalentHasGram != null && tx.equivalentHasGram !== undefined
        ? Math.abs(Number(tx.equivalentHasGram))
        : null;
    const hasGram =
      fromEq != null && !Number.isNaN(fromEq) && fromEq > 1e-9
        ? fromEq
        : Math.abs(Number(tx.netHasGram ?? tx.hasGram ?? 0));

    return { hasGram, cashTl: cash };
  }

  private escapeHtml(s: string): string {
    return s
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }
}
