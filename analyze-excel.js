const fs = require('fs');
const path = require('path');

const base = 'c:\\Users\\sdoqa\\OneDrive\\Masaüstü\\DÜKKAN YEDEK\\Dükkan';
const files = [
  { name: 'AlisSatis_Aralik2024', path: path.join(base, 'AlisSatis', 'Aralık2024.xlsx') },
  { name: 'Cariler', path: path.join(base, 'Cari', 'Cariler.xlsx') },
  { name: 'Kasa', path: path.join(base, 'Kasa', 'Kasa.xlsx') },
  { name: 'BorcSorgulama', path: path.join(base, 'Sahis', 'BorçSorgulama.xlsx') }
];

let xlsx;
try {
  xlsx = require('xlsx');
} catch (e) {
  console.log('Run: npm install xlsx');
  process.exit(1);
}

const out = [];

for (const f of files) {
  if (!fs.existsSync(f.path)) {
    out.push({ file: f.name, error: 'File not found: ' + f.path });
    continue;
  }
  const wb = xlsx.readFile(f.path, { cellFormula: true, cellStyles: false });
  const fileInfo = { file: f.name, sheets: [] };
  for (const sheetName of wb.SheetNames) {
    const ws = wb.Sheets[sheetName];
    const range = xlsx.utils.decode_range(ws['!ref'] || 'A1');
    const rows = [];
    const formulas = [];
    for (let R = range.s.r; R <= Math.min(range.e.r, 25); R++) {
      const row = [];
      for (let C = range.s.c; C <= range.e.c; C++) {
        const addr = xlsx.utils.encode_cell({ r: R, c: C });
        const cell = ws[addr];
        let val = '';
        let formula = '';
        if (cell) {
          if (cell.f) formula = cell.f;
          val = cell.v != null ? String(cell.v) : (cell.w != null ? cell.w : '');
        }
        row.push(val || '');
        if (formula) formulas.push({ cell: addr, formula });
      }
      rows.push(row);
    }
    fileInfo.sheets.push({
      name: sheetName,
      range: ws['!ref'],
      headerRow: rows[0] || [],
      sampleRows: rows.slice(1, 8),
      formulas: formulas.slice(0, 50)
    });
  }
  out.push(fileInfo);
}

console.log(JSON.stringify(out, null, 2));
