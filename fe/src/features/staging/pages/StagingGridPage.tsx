import { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import WmmsAppShell from '../../../components/layout/WmmsAppShell';
import { useWmms } from '../../auth/hooks/WmmsContext';
import { orderIntakeApi } from '../../../services/wmmsApi';
import { INITIAL_STAGING_GRID_ROWS } from '../../../constants/wmmsConstants';
import { hasCellError, validateStagingRowClient } from '../utils/stagingValidator';

interface ColumnVisibility {
  finishColor: boolean;
  fscRequired: boolean;
  technicalDrawing: boolean;
}

interface CustomerOption {
  customerId: number;
  customerCode: string;
  companyName: string;
  country?: string;
}

export default function StagingGridPage() {
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const { setOrderSpaces, setCurrentSpaceKey, appendAuditLog } = useWmms();

  // Chế độ nạp dữ liệu: 'EXCEL' (tải file .xlsx) hoặc 'MANUAL' (nhập tay trực tiếp)
  const [intakeMode, setIntakeMode] = useState<'EXCEL' | 'MANUAL'>('EXCEL');

  // Thông tin đơn hàng (PO Header)
  const [poNumber, setPoNumber] = useState('PO-2026-OCT-880515');
  const [selectedCustomerId, setSelectedCustomerId] = useState<number | ''>('');
  const [customerName, setCustomerName] = useState('Target Home US');
  const [customerCountry, setCustomerCountry] = useState('USA');
  const [uploadedFileName, setUploadedFileName] = useState('');
  const [sheetName, setSheetName] = useState('');

  // Tùy biến hiển thị cột (Column Visibility)
  const [showColumnDropdown, setShowColumnDropdown] = useState(false);
  const [visibleColumns, setVisibleColumns] = useState<ColumnVisibility>({
    finishColor: true,
    fscRequired: true,
    technicalDrawing: true,
  });

  // Danh sách dòng dữ liệu trên bảng Staging
  const [rows, setRows] = useState<any[]>(() =>
    INITIAL_STAGING_GRID_ROWS.map((r) => validateStagingRowClient(r))
  );

  const [bannerAlert, setBannerAlert] = useState<{
    type: 'error' | 'success' | 'info';
    text: string;
  } | null>(null);

  const [isValidating, setIsValidating] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isUploading, setIsUploading] = useState(false);

  // Lấy danh sách khách hàng B2B thực tế từ Backend
  const { data: customersRes } = useQuery({
    queryKey: ['customers'],
    queryFn: () => orderIntakeApi.getCustomers(),
    retry: false,
  });

  const customerList: CustomerOption[] = (customersRes?.data as CustomerOption[]) || [];

  // Tự động gán khách hàng mặc định khi danh sách tải xong
  useEffect(() => {
    if (customerList.length > 0 && selectedCustomerId === '') {
      setSelectedCustomerId(customerList[0].customerId);
      setCustomerName(customerList[0].companyName);
      setCustomerCountry(customerList[0].country || 'USA');
    }
  }, [customerList, selectedCustomerId]);

  const handleCustomerChange = (customerIdVal: string) => {
    const idNum = Number(customerIdVal);
    setSelectedCustomerId(idNum);
    const found = customerList.find((c) => c.customerId === idNum);
    if (found) {
      setCustomerName(found.companyName);
      setCustomerCountry(found.country || '');
    }
  };

  // Tính tổng số ô lỗi đỏ trên toàn bộ lưới (BR-ORD-02)
  const totalErrorCells = rows.reduce(
    (sum, r) => sum + (r.cellErrors?.length || 0),
    0
  );
  const canConfirmOrder = rows.length > 0 && totalErrorCells === 0;

  /** Cập nhật giá trị ô khi người dùng gõ sửa inline */
  const handleCellChange = (rowIndex: number, field: string, value: any) => {
    setRows((prev) =>
      prev.map((row, idx) => {
        if (idx !== rowIndex) return row;
        const updatedRow = { ...row, [field]: value };
        return validateStagingRowClient(updatedRow);
      })
    );
  };

  /** Thêm một dòng mới vào bảng */
  const handleAddRow = () => {
    const nextNum = rows.length + 1;
    const newRow = validateStagingRowClient({
      rowNumber: nextNum,
      productSku: `SKU-PROD-0${nextNum}`,
      productDescription: 'Solid Wood Dining Chair',
      dimensionsMm: '450 x 520 x 850',
      finishColor: 'Walnut Stain',
      orderQty: 100,
      targetEtd: new Date(Date.now() + 30 * 86400000).toISOString().split('T')[0],
      fscRequired: true,
      technicalDrawingName: `CAD_Chair_0${nextNum}.pdf`,
    });
    setRows((prev) => [...prev, newRow]);
  };

  /** Xóa một dòng cụ thể theo index */
  const handleDeleteRowAt = (indexToRemove: number) => {
    if (rows.length <= 1) {
      setBannerAlert({
        type: 'error',
        text: 'Lưới Staging phải giữ lại ít nhất 1 dòng sản phẩm.',
      });
      return;
    }
    setRows((prev) =>
      prev
        .filter((_, idx) => idx !== indexToRemove)
        .map((r, i) => validateStagingRowClient({ ...r, rowNumber: i + 1 }))
    );
  };

  /** Chuyển sang chế độ nhập tay với lưới trống */
  const handleSwitchToManualMode = () => {
    setIntakeMode('MANUAL');
    setUploadedFileName('');
    setSheetName('');
    const initialBlankRow = validateStagingRowClient({
      rowNumber: 1,
      productSku: '',
      productDescription: '',
      dimensionsMm: '',
      finishColor: '',
      orderQty: 0,
      targetEtd: new Date().toISOString().split('T')[0],
      fscRequired: true,
      technicalDrawingName: '',
    });
    setRows([initialBlankRow]);
    setBannerAlert({
      type: 'info',
      text: 'Đã chuyển sang chế độ Nhập tay (Manual Entry). Hãy nhập thông tin sản phẩm và bấm "+ Thêm dòng".',
    });
  };

  /** Nạp dữ liệu mẫu thử nghiệm */
  const handleLoadSampleData = () => {
    setUploadedFileName('PO_Target_Home_US_880515.xlsx (Sample Demo)');
    setSheetName('OrderDetails');
    setPoNumber('ASH-2026-OCT-880515');
    setCustomerName('Target Home US');
    setCustomerCountry('USA');
    setRows(INITIAL_STAGING_GRID_ROWS.map((r) => validateStagingRowClient(r)));
    setBannerAlert({
      type: 'success',
      text: 'Đã nạp 4 dòng dữ liệu mẫu chuẩn Report 3. Các ô lỗi cú pháp được đánh dấu viền đỏ.',
    });
  };

  /** Upload file Excel PO lên Backend (/api/v1/orders/staging/upload) */
  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setIntakeMode('EXCEL');
    setUploadedFileName(file.name);
    setIsUploading(true);

    try {
      const apiRes = await orderIntakeApi.uploadExcelPoToStaging(
        file,
        poNumber.trim() || null,
        selectedCustomerId || null
      );

      const data = apiRes?.data;
      if (data && Array.isArray(data.rows)) {
        setRows(data.rows.map((r: any) => validateStagingRowClient(r)));
        setSheetName(data.sheetName || 'Worksheet');
        if (data.poNumber) setPoNumber(data.poNumber);
        if (data.customerName) setCustomerName(data.customerName);

        setBannerAlert({
          type: data.canConfirm ? 'success' : 'error',
          text: data.summaryMessage || `Đã phân tích ${data.totalRows} dòng từ file ${file.name}.`,
        });
        toast.success(`Upload file "${file.name}" thành công!`);
        return;
      }
    } catch (err: any) {
      console.warn('Backend parse error, falling back to local validation:', err);
      // Fallback cục bộ
      setBannerAlert({
        type: 'error',
        text:
          err?.response?.data?.message ||
          'Không thể kết nối Backend để bóc tách file Excel. Đã nạp dữ liệu vào lưới cục bộ.',
      });
    } finally {
      setIsUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  /** Nút "Validate Grid Again": Gửi lên Backend hoặc kiểm tra client */
  const handleValidateGridAgain = async () => {
    setIsValidating(true);
    const revalidated = rows.map((r, i) =>
      validateStagingRowClient({ ...r, rowNumber: i + 1 })
    );
    setRows(revalidated);

    try {
      const apiRes = await orderIntakeApi.validateStagingGrid({
        fileName: uploadedFileName || 'Manual_Staging_Grid.xlsx',
        poNumber: poNumber.trim() || 'PO-NEW',
        customerId: selectedCustomerId || null,
        customerName: customerName.trim(),
        rows: revalidated,
      });

      if (apiRes?.data?.rows) {
        setRows(apiRes.data.rows.map((r: any) => validateStagingRowClient(r)));
      }

      const errCount = apiRes?.data?.errorCellCount ?? 0;
      if (errCount === 0) {
        setBannerAlert({
          type: 'success',
          text: `Toàn bộ ${revalidated.length} dòng sản phẩm đều chuẩn xác cú pháp! Đã sẵn sàng bấm "Confirm & Create Order Space".`,
        });
        toast.success('Kiểm tra dữ liệu thành công!');
      } else {
        setBannerAlert({
          type: 'error',
          text: `Phát hiện ${errCount} lỗi định dạng (bôi đỏ). Vui lòng sửa trực tiếp trên ô trước khi xác nhận đơn.`,
        });
        toast.error(`Còn ${errCount} ô lỗi định dạng cần sửa.`);
      }
    } catch {
      // Client-side fallback check
      const remainingErrors = revalidated.reduce(
        (sum, r) => sum + (r.cellErrors?.length || 0),
        0
      );
      if (remainingErrors === 0) {
        setBannerAlert({
          type: 'success',
          text: `Toàn bộ ${revalidated.length} dòng sản phẩm đã sạch lỗi (Client-side validation).`,
        });
      } else {
        setBannerAlert({
          type: 'error',
          text: `Còn ${remainingErrors} ô lỗi định dạng cần chỉnh sửa trực tiếp.`,
        });
      }
    } finally {
      setIsValidating(false);
    }
  };

  /** Nút "Confirm & Create Order Space": Xác nhận đơn và tạo Order Space */
  const handleConfirmAndCreateOrderSpace = async () => {
    if (!canConfirmOrder) return;

    if (!poNumber.trim()) {
      toast.error('Vui lòng nhập Mã PO (Purchase Order Number)!');
      return;
    }

    setIsSubmitting(true);
    const finalRows = rows.map((r, i) =>
      validateStagingRowClient({ ...r, rowNumber: i + 1 })
    );

    try {
      const apiRes = await orderIntakeApi.confirmSalesOrder({
        poNumber: poNumber.trim(),
        customerId: selectedCustomerId || null,
        customerName: customerName.trim() || 'Direct Customer',
        customerCountry: customerCountry.trim() || 'Vietnam',
        targetEtd: finalRows[0]?.targetEtd || new Date().toISOString(),
        rows: finalRows,
      });

      const createdSpace = apiRes?.data;
      const spaceKey = createdSpace?.spaceKey || `SPACE-${poNumber.trim().toUpperCase()}`;

      // Thêm vào context orderSpaces để thanh Dropdown Header cập nhật ngay
      const totalUnits = finalRows.reduce(
        (sum, r) => sum + Number(r.orderQty || 0),
        0
      );

      const newSpaceItem = {
        spaceKey,
        spaceLabel: `${spaceKey} (${customerName})`,
        customerName,
        poNumber: poNumber.trim(),
        orderDate: new Date().toISOString().split('T')[0],
        etdDate: finalRows[0]?.targetEtd || '2026-11-30',
        daysRemaining: 30,
        isUrgentEtd: false,
        totalOrderedUnits: totalUnits,
        lineItemsVolumeSummary: `${totalUnits} Pcs (${finalRows.length} SKUs)`,
        lifecycleStatusBadge: 'NEWLY_RECEIVED',
        overallProgressPct: 0,
        customRows: finalRows,
      };

      setOrderSpaces((prev) => {
        const exists = prev.some((s) => s.spaceKey === spaceKey);
        if (exists) {
          return prev.map((s) => (s.spaceKey === spaceKey ? newSpaceItem : s));
        }
        return [newSpaceItem, ...prev];
      });

      appendAuditLog(
        'CONFIRM_PO_STAGING',
        `Order Space #${spaceKey}`,
        'STAGING_RAW → NEWLY_RECEIVED',
        `Created space for PO ${poNumber.trim()} with ${totalUnits} units across ${finalRows.length} line items.`
      );

      setCurrentSpaceKey(spaceKey);
      toast.success(`Khởi tạo Không gian đơn hàng ${spaceKey} thành công!`);
      navigate(`/orders/spaces/${spaceKey}`);
    } catch (err: any) {
      const msg =
        err?.response?.data?.message ||
        'Không thể khởi tạo Không gian đơn hàng. Vui lòng kiểm tra lại thông tin đơn.';
      setBannerAlert({ type: 'error', text: msg });
      toast.error(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <WmmsAppShell
      mode="staging"
      customTopLeft={
        <span className="text-xs font-bold text-blue-600 tracking-tight flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
          Staging Isolation Sandbox (Data Sanitization)
        </span>
      }
    >
      <div className="max-w-[1400px] mx-auto space-y-5">
        {/* Hàng tiêu đề + Nút thao tác chính */}
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
            <h1 className="text-lg font-extrabold text-slate-900 tracking-tight">
              Safe Excel PO Staging Grid (SCR-ORD-02)
            </h1>
            <p className="text-xs text-slate-500 mt-0.5">
              Tự động làm sạch thông tin thương mại nhạy cảm và đánh dấu đỏ các ô lỗi định dạng trước khi lưu vào CSDL.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            {/* Field #1: Validate Grid Again */}
            <button
              type="button"
              disabled={isValidating}
              onClick={handleValidateGridAgain}
              className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md shadow-sm transition flex items-center gap-1.5"
            >
              <span>{isValidating ? '⏳' : '🔄'}</span>
              <span>{isValidating ? 'Validating...' : 'Validate Grid Again'}</span>
            </button>

            {/* Field #2: Confirm & Create Order Space */}
            <button
              type="button"
              disabled={!canConfirmOrder || isSubmitting}
              onClick={handleConfirmAndCreateOrderSpace}
              className={`px-4 py-2 text-xs font-semibold rounded-md shadow-sm transition flex items-center gap-1.5 ${
                canConfirmOrder
                  ? 'bg-[#16a34a] hover:bg-emerald-700 text-white cursor-pointer'
                  : 'bg-slate-300 text-slate-500 cursor-not-allowed'
              }`}
            >
              <span>✓</span>
              <span>
                {isSubmitting
                  ? 'Initializing Order Space...'
                  : canConfirmOrder
                  ? 'Confirm & Create Order Space (Ready)'
                  : `Confirm & Create Order Space (${totalErrorCells} Errors)`}
              </span>
            </button>
          </div>
        </div>

        {/* Thanh Thông tin Khách hàng & Mã PO */}
        <div className="bg-white rounded-lg border border-slate-200 p-4 shadow-sm">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            {/* Chọn khách hàng B2B */}
            <div>
              <label className="block text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-1">
                Khách hàng B2B (Customer)
              </label>
              {customerList.length > 0 ? (
                <select
                  value={selectedCustomerId}
                  onChange={(e) => handleCustomerChange(e.target.value)}
                  className="w-full px-3 py-1.5 text-xs rounded border border-slate-300 bg-white text-slate-800 font-medium focus:outline-none focus:border-blue-500"
                >
                  {customerList.map((c) => (
                    <option key={c.customerId} value={c.customerId}>
                      {c.companyName} ({c.country || 'N/A'})
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  type="text"
                  value={customerName}
                  onChange={(e) => setCustomerName(e.target.value)}
                  placeholder="Target Home US"
                  className="w-full px-3 py-1.5 text-xs rounded border border-slate-300 bg-white text-slate-800 focus:outline-none focus:border-blue-500"
                />
              )}
            </div>

            {/* Mã PO */}
            <div>
              <label className="block text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-1">
                Mã Đơn hàng (PO Number) *
              </label>
              <input
                type="text"
                value={poNumber}
                onChange={(e) => setPoNumber(e.target.value)}
                placeholder="Ví dụ: PO-2026-OCT-880515"
                className="w-full px-3 py-1.5 text-xs rounded border border-slate-300 bg-white text-slate-800 font-mono font-semibold focus:outline-none focus:border-blue-500"
              />
            </div>

            {/* Quốc gia khách hàng */}
            <div>
              <label className="block text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-1">
                Quốc gia đến (Destination)
              </label>
              <input
                type="text"
                value={customerCountry}
                onChange={(e) => setCustomerCountry(e.target.value)}
                placeholder="USA / EU / Vietnam"
                className="w-full px-3 py-1.5 text-xs rounded border border-slate-300 bg-white text-slate-800 focus:outline-none focus:border-blue-500"
              />
            </div>

            {/* Lựa chọn phương thức nạp */}
            <div>
              <label className="block text-[11px] font-bold uppercase tracking-wider text-slate-500 mb-1">
                Phương thức nạp dữ liệu
              </label>
              <div className="flex items-center gap-1.5 mt-0.5">
                <button
                  type="button"
                  onClick={() => setIntakeMode('EXCEL')}
                  className={`flex-1 py-1.5 px-2 text-xs font-semibold rounded border transition ${
                    intakeMode === 'EXCEL'
                      ? 'bg-blue-50 text-blue-700 border-blue-300 shadow-2xs'
                      : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'
                  }`}
                >
                  📁 Nạp Excel
                </button>
                <button
                  type="button"
                  onClick={handleSwitchToManualMode}
                  className={`flex-1 py-1.5 px-2 text-xs font-semibold rounded border transition ${
                    intakeMode === 'MANUAL'
                      ? 'bg-blue-50 text-blue-700 border-blue-300 shadow-2xs'
                      : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'
                  }`}
                >
                  ✍️ Nhập tay
                </button>
              </div>
            </div>
          </div>
        </div>

        {/* Thông báo kết quả kiểm tra nếu có */}
        {bannerAlert && (
          <div
            className={`px-4 py-2.5 rounded-md text-xs font-medium flex items-center justify-between shadow-2xs ${
              bannerAlert.type === 'error'
                ? 'bg-red-50 text-red-700 border border-red-200'
                : bannerAlert.type === 'info'
                ? 'bg-blue-50 text-blue-700 border border-blue-200'
                : 'bg-emerald-50 text-emerald-800 border border-emerald-200'
            }`}
          >
            <span>{bannerAlert.text}</span>
            <button
              type="button"
              onClick={() => setBannerAlert(null)}
              className="font-bold ml-4 text-sm leading-none hover:opacity-75"
            >
              ×
            </button>
          </div>
        )}

        {/* Khu vực Upload file Excel (chỉ hiện khi ở chế độ EXCEL) */}
        {intakeMode === 'EXCEL' && (
          <div className="bg-slate-50/70 hover:bg-slate-100/70 border-2 border-dashed border-blue-200 rounded-xl p-5 text-center transition">
            <input
              ref={fileInputRef}
              type="file"
              accept=".xlsx,.xls"
              onChange={handleFileUpload}
              className="hidden"
            />
            <div className="text-2xl mb-1">📄</div>
            <div className="text-xs font-bold text-slate-900">
              {uploadedFileName
                ? `Tệp đã tải lên: ${uploadedFileName} ${sheetName ? `(Sheet: ${sheetName})` : ''}`
                : 'Kéo thả hoặc chọn file Excel PO (.xlsx / .xls) từ máy tính'}
            </div>
            <p className="text-[11px] text-slate-500 mt-1 max-w-xl mx-auto">
              Hệ thống tự động loại bỏ các cột nhạy cảm{' '}
              <span className="font-bold text-slate-700">
                Container No, Shipping Port, Proforma Invoice (PI), Unit Prices (CBD)
              </span>{' '}
              để đảm bảo tính toàn vẹn và bảo mật dữ liệu xưởng.
            </p>

            <div className="mt-3 flex items-center justify-center gap-2">
              <button
                type="button"
                disabled={isUploading}
                onClick={() => fileInputRef.current?.click()}
                className="px-3.5 py-1.5 text-xs font-semibold bg-[#2563eb] hover:bg-blue-700 text-white rounded-md shadow-sm transition"
              >
                {isUploading ? 'Đang đọc file...' : 'Chọn file Excel từ máy'}
              </button>
              <button
                type="button"
                onClick={handleLoadSampleData}
                className="px-3 py-1.5 text-xs font-medium bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 rounded-md transition"
              >
                Nạp dữ liệu mẫu thử nghiệm
              </button>
            </div>
          </div>
        )}

        {/* Khung bảng Staging Grid Table */}
        <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
          {/* Thanh công cụ: Legend + Tùy biến hiển thị cột + Nút thêm dòng */}
          <div className="px-5 py-3 border-b border-slate-200 flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-4 text-[11px] text-slate-600">
              <span className="flex items-center gap-1.5">
                <span className="w-3 h-3 rounded-sm bg-rose-50 border border-rose-400 inline-block" />
                <span>Viền đỏ = Ô lỗi định dạng</span>
              </span>
              <span className="flex items-center gap-1.5">
                <span className="w-3 h-3 rounded-sm bg-white border border-slate-300 inline-block" />
                <span>Ô trắng = Dữ liệu hợp lệ</span>
              </span>
              <span className="text-slate-400">|</span>
              <span className="font-semibold text-slate-700">
                Tổng cộng: {rows.length} dòng sản phẩm
              </span>
            </div>

            <div className="flex items-center gap-2 relative">
              {/* Dropdown Tùy biến Ẩn/Hiện Cột (Column Visibility) */}
              <div className="relative">
                <button
                  type="button"
                  onClick={() => setShowColumnDropdown(!showColumnDropdown)}
                  className="px-2.5 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 flex items-center gap-1"
                >
                  <span>👁️ Ẩn/Hiện cột</span>
                  <span className="text-[9px]">▼</span>
                </button>

                {showColumnDropdown && (
                  <div className="absolute right-0 mt-1 w-48 bg-white border border-slate-200 rounded-md shadow-lg py-2 z-20 text-xs">
                    <div className="px-3 py-1 text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                      Cột tùy chọn
                    </div>
                    <label className="flex items-center gap-2 px-3 py-1.5 hover:bg-slate-50 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={visibleColumns.finishColor}
                        onChange={(e) =>
                          setVisibleColumns({ ...visibleColumns, finishColor: e.target.checked })
                        }
                        className="rounded text-blue-600"
                      />
                      <span>Finish / Color</span>
                    </label>
                    <label className="flex items-center gap-2 px-3 py-1.5 hover:bg-slate-50 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={visibleColumns.fscRequired}
                        onChange={(e) =>
                          setVisibleColumns({ ...visibleColumns, fscRequired: e.target.checked })
                        }
                        className="rounded text-blue-600"
                      />
                      <span>FSC Standard</span>
                    </label>
                    <label className="flex items-center gap-2 px-3 py-1.5 hover:bg-slate-50 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={visibleColumns.technicalDrawing}
                        onChange={(e) =>
                          setVisibleColumns({
                            ...visibleColumns,
                            technicalDrawing: e.target.checked,
                          })
                        }
                        className="rounded text-blue-600"
                      />
                      <span>Technical Drawing</span>
                    </label>
                  </div>
                )}
              </div>

              {/* Nút + Thêm dòng sản phẩm */}
              <button
                type="button"
                onClick={handleAddRow}
                className="px-2.5 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-300 rounded hover:bg-slate-50 flex items-center gap-1"
              >
                <span>+</span> Thêm dòng
              </button>
            </div>
          </div>

          {/* Interactive Staging Grid Table */}
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-[11px] font-semibold text-slate-400 bg-slate-50/50">
                  <th className="py-3 px-3 w-10 text-center">#</th>
                  <th className="py-3 px-3 w-44">Internal Product SKU *</th>
                  <th className="py-3 px-3 min-w-[200px]">Product Description *</th>
                  <th className="py-3 px-3 w-48">Dimensions (L x W x H mm) *</th>
                  {visibleColumns.finishColor && (
                    <th className="py-3 px-3 w-36">Finish / Color</th>
                  )}
                  <th className="py-3 px-3 w-28">Order Qty *</th>
                  <th className="py-3 px-3 w-36">Target ETD *</th>
                  {visibleColumns.fscRequired && (
                    <th className="py-3 px-3 w-28">FSC Standard</th>
                  )}
                  {visibleColumns.technicalDrawing && (
                    <th className="py-3 px-4 min-w-[150px]">Technical Drawing</th>
                  )}
                  <th className="py-3 px-3 w-12 text-center">Xóa</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-xs">
                {rows.map((row, idx) => (
                  <tr key={idx} className="hover:bg-slate-50/60">
                    <td className="py-3 px-3 text-slate-500 font-medium text-center">
                      {idx + 1}
                    </td>

                    {/* Internal Product SKU */}
                    <td className="py-2 px-3">
                      <input
                        type="text"
                        value={row.productSku || ''}
                        placeholder="Mã SKU"
                        onChange={(e) => handleCellChange(idx, 'productSku', e.target.value)}
                        className={`w-full px-2.5 py-1.5 text-xs rounded border focus:outline-none ${
                          hasCellError(row, 'productSku')
                            ? 'bg-rose-50 border-rose-500 text-rose-800 font-semibold'
                            : 'bg-white border-slate-300 text-slate-800'
                        }`}
                      />
                    </td>

                    {/* Product Description */}
                    <td className="py-2 px-3">
                      <input
                        type="text"
                        value={row.productDescription || ''}
                        placeholder="Tên / Mô tả sản phẩm"
                        onChange={(e) =>
                          handleCellChange(idx, 'productDescription', e.target.value)
                        }
                        className={`w-full px-2.5 py-1.5 text-xs rounded border focus:outline-none ${
                          hasCellError(row, 'productDescription')
                            ? 'bg-rose-50 border-rose-500 text-rose-800 font-semibold'
                            : 'bg-white border-slate-300 text-slate-800'
                        }`}
                      />
                    </td>

                    {/* Dimensions (L x W x H mm) */}
                    <td className="py-2 px-3">
                      <input
                        type="text"
                        value={row.dimensionsMm || ''}
                        placeholder="1600 x 450 x 800"
                        title="Định dạng Dài x Rộng x Cao (ví dụ: 1600 x 450 x 800)"
                        onChange={(e) => handleCellChange(idx, 'dimensionsMm', e.target.value)}
                        className={`w-full px-2.5 py-1.5 text-xs rounded border focus:outline-none ${
                          hasCellError(row, 'dimensionsMm')
                            ? 'bg-rose-50 border-rose-500 text-rose-800 font-semibold'
                            : 'bg-white border-slate-300 text-slate-800'
                        }`}
                      />
                    </td>

                    {/* Finish / Color */}
                    {visibleColumns.finishColor && (
                      <td className="py-2 px-3">
                        <input
                          type="text"
                          value={row.finishColor || ''}
                          placeholder="Màu sơn / Hoàn thiện"
                          onChange={(e) => handleCellChange(idx, 'finishColor', e.target.value)}
                          className={`w-full px-2.5 py-1.5 text-xs rounded border focus:outline-none ${
                            hasCellError(row, 'finishColor')
                              ? 'bg-rose-50 border-rose-500 text-rose-800 font-semibold'
                              : 'bg-white border-slate-300 text-slate-800'
                          }`}
                        />
                      </td>
                    )}

                    {/* Order Qty */}
                    <td className="py-2 px-3">
                      <input
                        type="number"
                        min="1"
                        value={row.orderQty ?? ''}
                        onChange={(e) => handleCellChange(idx, 'orderQty', e.target.value)}
                        className={`w-full px-2.5 py-1.5 text-xs rounded border focus:outline-none ${
                          hasCellError(row, 'orderQty')
                            ? 'bg-rose-50 border-rose-500 text-rose-800 font-semibold'
                            : 'bg-white border-slate-300 text-slate-800 font-semibold'
                        }`}
                      />
                    </td>

                    {/* Customer Target ETD */}
                    <td className="py-2 px-3">
                      <input
                        type="date"
                        value={row.targetEtd ? String(row.targetEtd).split('T')[0] : ''}
                        onChange={(e) => handleCellChange(idx, 'targetEtd', e.target.value)}
                        className={`w-full px-2.5 py-1.5 text-xs rounded border focus:outline-none ${
                          hasCellError(row, 'targetEtd')
                            ? 'bg-rose-50 border-rose-500 text-rose-800 font-semibold'
                            : 'bg-white border-slate-300 text-slate-800'
                        }`}
                      />
                    </td>

                    {/* FSC Standard */}
                    {visibleColumns.fscRequired && (
                      <td className="py-2 px-3">
                        <select
                          value={row.fscRequired ? 'true' : 'false'}
                          onChange={(e) =>
                            handleCellChange(idx, 'fscRequired', e.target.value === 'true')
                          }
                          className="w-full px-2 py-1.5 text-xs rounded border border-slate-300 bg-white font-medium"
                        >
                          <option value="true">FSC 100%</option>
                          <option value="false">None</option>
                        </select>
                      </td>
                    )}

                    {/* Technical Drawing */}
                    {visibleColumns.technicalDrawing && (
                      <td className="py-2 px-4">
                        <input
                          type="text"
                          value={row.technicalDrawingName || ''}
                          placeholder="CAD_Drawing.pdf"
                          onChange={(e) =>
                            handleCellChange(idx, 'technicalDrawingName', e.target.value)
                          }
                          className="w-full px-2.5 py-1.5 text-xs rounded border border-slate-300 bg-white text-slate-800 font-mono text-[11px] focus:outline-none"
                        />
                      </td>
                    )}

                    {/* Nút xóa dòng */}
                    <td className="py-2 px-3 text-center">
                      <button
                        type="button"
                        onClick={() => handleDeleteRowAt(idx)}
                        title="Xóa dòng này"
                        className="text-slate-400 hover:text-rose-600 transition p-1"
                      >
                        🗑️
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </WmmsAppShell>
  );
}
