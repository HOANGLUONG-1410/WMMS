/**
 * Các hàm tiện ích kiểm tra định dạng dữ liệu trên bảng Safe Excel PO Staging Grid (UC-30, UC-32, BR-ORD-02)
 */

const DIMENSION_REGEX = /^\s*(\d+(?:\.\d+)?)\s*[xX*×]\s*(\d+(?:\.\d+)?)\s*[xX*×]\s*(\d+(?:\.\d+)?)\s*(?:mm|cm)?\s*$/;
const ISO_DATE_REGEX = /^\d{4}-\d{2}-\d{2}$/;

/**
 * Kiểm tra hợp lệ một dòng sản phẩm trên bảng Staging Grid (SCR-ORD-02).
 * Trả về danh sách các trường bị lỗi để UI hiển thị viền đỏ (Red Cells = Syntax / Format Error).
 */
export function validateStagingRowClient(row) {
  const cellErrors = [];

  if (!row.productSku || !String(row.productSku).trim()) {
    cellErrors.push({ fieldName: 'productSku', errorMessage: 'Bắt buộc nhập mã SKU nội bộ' });
  }

  if (!row.productDescription || !String(row.productDescription).trim()) {
    cellErrors.push({ fieldName: 'productDescription', errorMessage: 'Bắt buộc nhập mô tả sản phẩm' });
  }

  if (!row.dimensionsMm || !DIMENSION_REGEX.test(String(row.dimensionsMm))) {
    cellErrors.push({
      fieldName: 'dimensionsMm',
      errorMessage: 'Định dạng D x R x C mm (VD: 450 x 520 x 850)'
    });
  }

  if (!row.finishColor || !String(row.finishColor).trim()) {
    cellErrors.push({ fieldName: 'finishColor', errorMessage: 'Bắt buộc nhập hoàn thiện / màu' });
  }

  const qty = Number(row.orderQty);
  if (!Number.isFinite(qty) || qty <= 0) {
    cellErrors.push({ fieldName: 'orderQty', errorMessage: 'Số lượng đặt phải > 0' });
  }

  const etdStr = String(row.targetEtd || '').trim();
  const parsedDate = new Date(etdStr);
  if (!ISO_DATE_REGEX.test(etdStr) || Number.isNaN(parsedDate.getTime())) {
    cellErrors.push({ fieldName: 'targetEtd', errorMessage: 'Ngày ETD không hợp lệ (YYYY-MM-DD)' });
  }

  return {
    ...row,
    cellErrors,
    isRowValid: cellErrors.length === 0
  };
}

/**
 * Kiểm tra một ô cụ thể trong dòng có bị lỗi định dạng hay không
 */
export function hasCellError(row, fieldName: string) {
  if (!row || !Array.isArray(row.cellErrors)) return false;
  return row.cellErrors.some(
    (err) => String(err.fieldName).toLowerCase() === String(fieldName).toLowerCase()
  );
}
