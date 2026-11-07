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
      productDescription: 'Ghế ăn gỗ nguyên khối',
      dimensionsMm: '450 x 520 x 850',
      finishColor: 'Nhuộm màu óc chó',
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
    setUploadedFileName('PO_Target_Home_US_880515.xlsx (Dữ liệu mẫu)');
    setSheetName('OrderDetails');
    setPoNumber('ASH-2026-OCT-880515');
    setCustomerName('Target Home US');
    setCustomerCountry('USA');
    setRows(INITIAL_STAGING_GRID_ROWS.map((r) => validateStagingRowClient(r)));
    setBannerAlert({
      type: 'success',
      text: 'Đã nạp 4 dòng dữ liệu mẫu. Các ô lỗi cú pháp được đánh dấu viền đỏ.',
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
        setSheetName(data.sheetName || 'Trang tính');
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
      console.warn('Lỗi phân tích từ máy chủ, chuyển sang kiểm tra cục bộ:', err);
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

  /** Nút "Kiểm tra lại lưới": Gửi lên Backend hoặc kiểm tra client */
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
        customerName: customerName.trim() || 'Khách hàng trực tiếp',
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
        lineItemsVolumeSummary: `${totalUnits} cái (${finalRows.length} SKU)`,
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
        `Đã tạo Space cho PO ${poNumber.trim()} gồm ${totalUnits} sản phẩm trên ${finalRows.length} dòng hàng.`
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
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => navigate('/orders')}
            className="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-sm font-semibold text-slate-700 bg-white border border-slate-300 hover:bg-slate-50 transition shadow-2xs cursor-pointer"
          >
            <svg className="w-4 h-4 text-slate-500" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
            </svg>
            <span>Danh sách đơn hàng</span>
          </button>
          <span className="hidden sm:inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-bold bg-blue-50 text-blue-700 border border-blue-200">
            <span className="w-1.5 h-1.5 rounded-full bg-blue-600"></span>
            Khu vực Staging PO
          </span>
        </div>
      }
    >
      <div className="max-w-[1400px] mx-auto space-y-6">
        {/* Page Heading + Action Buttons */}
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
          <div>
            <h1 className="text-xl md:text-2xl font-extrabold text-slate-900 tracking-tight">
              Tiếp nhận &amp; Làm sạch PO Excel (Staging)
            </h1>
            <p className="text-sm text-slate-600 mt-1">
              Tự động lọc bỏ thông tin thương mại nhạy cảm và kiểm tra định dạng dữ liệu trước khi khởi tạo Order Space.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <button
              type="button"
              disabled={isValidating}
              onClick={handleValidateGridAgain}
              className="px-4 py-2.5 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg shadow-xs transition flex items-center gap-2 cursor-pointer"
            >
              <svg className="w-4 h-4 text-slate-500" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              <span>{isValidating ? 'Đang kiểm tra...' : 'Kiểm tra lại dữ liệu'}</span>
            </button>

            <button
              type="button"
              disabled={!canConfirmOrder || isSubmitting}
              onClick={handleConfirmAndCreateOrderSpace}
              className={`px-4 py-2.5 text-sm font-semibold rounded-lg shadow-xs transition flex items-center gap-2 ${
                canConfirmOrder
                  ? 'bg-blue-600 hover:bg-blue-700 text-white cursor-pointer'
                  : 'bg-slate-200 text-slate-500 cursor-not-allowed'
              }`}
            >
              <svg className="w-4 h-4 shrink-0" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
              </svg>
              <span>
                {isSubmitting
                  ? 'Đang khởi tạo Order Space...'
                  : canConfirmOrder
                  ? 'Xác nhận & Tạo Order Space'
                  : `Còn ${totalErrorCells} lỗi định dạng cần sửa`}
              </span>
            </button>
          </div>
        </div>

        {/* Customer & PO Metadata Card */}
        <div className="bg-white rounded-xl border border-slate-200 p-6 shadow-xs">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-5">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Khách hàng B2B
              </label>
              {customerList.length > 0 ? (
                <select
                  value={selectedCustomerId}
                  onChange={(e) => handleCustomerChange(e.target.value)}
                  className="w-full px-3.5 py-2 text-sm rounded-lg border border-slate-300 bg-white text-slate-800 font-medium focus:outline-none focus:ring-2 focus:ring-blue-600"
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
                  className="w-full px-3.5 py-2 text-sm rounded-lg border border-slate-300 bg-white text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-600"
                />
              )}
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Mã đơn hàng (PO Number) <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                value={poNumber}
                onChange={(e) => setPoNumber(e.target.value)}
                placeholder="VD: PO-2026-OCT-880515"
                className="w-full px-3.5 py-2 text-sm rounded-lg border border-slate-300 bg-white text-slate-800 font-mono font-semibold focus:outline-none focus:ring-2 focus:ring-blue-600"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Quốc gia xuất khẩu
              </label>
              <input
                type="text"
                value={customerCountry}
                onChange={(e) => setCustomerCountry(e.target.value)}
                placeholder="Mỹ / EU / Việt Nam"
                className="w-full px-3.5 py-2 text-sm rounded-lg border border-slate-300 bg-white text-slate-800 focus:outline-none focus:ring-2 focus:ring-blue-600"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1.5">
                Phương thức nhập liệu
              </label>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => setIntakeMode('EXCEL')}
                  className={`flex-1 py-2 px-3 text-sm font-semibold rounded-lg border transition cursor-pointer ${
                    intakeMode === 'EXCEL'
                      ? 'bg-blue-50 text-blue-700 border-blue-300'
                      : 'bg-white text-slate-700 border-slate-300 hover:bg-slate-50'
                  }`}
                >
                  Tải file Excel
                </button>
                <button
                  type="button"
                  onClick={handleSwitchToManualMode}
                  className={`flex-1 py-2 px-3 text-sm font-semibold rounded-lg border transition cursor-pointer ${
                    intakeMode === 'MANUAL'
                      ? 'bg-blue-50 text-blue-700 border-blue-300'
                      : 'bg-white text-slate-700 border-slate-300 hover:bg-slate-50'
                  }`}
                >
                  Nhập tay
                </button>
              </div>
            </div>
          </div>
        </div>

        {/* Alert Banner */}
        {bannerAlert && (
          <div
            className={`px-4 py-3 rounded-lg text-sm font-medium flex items-center justify-between shadow-xs ${
              bannerAlert.type === 'error'
                ? 'bg-rose-50 text-rose-700 border border-rose-200'
                : bannerAlert.type === 'info'
                ? 'bg-blue-50 text-blue-700 border border-blue-200'
                : 'bg-emerald-50 text-emerald-800 border border-emerald-200'
            }`}
          >
            <span>{bannerAlert.text}</span>
            <button
              type="button"
              onClick={() => setBannerAlert(null)}
              className="font-bold ml-4 text-base leading-none hover:opacity-75 cursor-pointer"
            >
              ×
            </button>
          </div>
        )}

        {/* Excel Upload Dropzone */}
        {intakeMode === 'EXCEL' && (
          <div className="bg-white border-2 border-dashed border-blue-300 rounded-xl p-6 text-center transition">
            <input
              ref={fileInputRef}
              type="file"
              accept=".xlsx,.xls"
              onChange={handleFileUpload}
              className="hidden"
            />
            <div className="w-10 h-10 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center mx-auto mb-2">
              <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
              </svg>
            </div>
            <div className="text-sm font-bold text-slate-900">
              {uploadedFileName
                ? `Tệp đã tải lên: ${uploadedFileName} ${sheetName ? `(Trang tính: ${sheetName})` : ''}`
                : 'Kéo thả hoặc chọn file Excel PO (.xlsx / .xls) từ máy tính'}
            </div>
            <p className="text-xs text-slate-500 mt-1 max-w-xl mx-auto">
              Hệ thống tự động loại bỏ các cột thương mại nhạy cảm{' '}
              <span className="font-semibold text-slate-700">
                (Số container, Cảng xuất, Hóa đơn chiếu lệ PI, Đơn giá CBD)
              </span>{' '}
              để bảo mật dữ liệu khi chuyển xuống xưởng.
            </p>

            <div className="mt-4 flex items-center justify-center gap-3">
              <button
                type="button"
                disabled={isUploading}
                onClick={() => fileInputRef.current?.click()}
                className="px-4 py-2 text-sm font-semibold bg-blue-600 hover:bg-blue-700 text-white rounded-lg shadow-xs transition cursor-pointer"
              >
                {isUploading ? 'Đang đọc file...' : 'Chọn file Excel từ máy'}
              </button>
              <button
                type="button"
                onClick={handleLoadSampleData}
                className="px-4 py-2 text-sm font-semibold bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 rounded-lg transition cursor-pointer"
              >
                Nạp dữ liệu mẫu
              </button>
            </div>
          </div>
        )}

        {/* Staging Grid Table Card */}
        <div className="bg-white rounded-xl border border-slate-200 shadow-xs overflow-hidden">
          <div className="px-6 py-4 border-b border-slate-200 flex flex-wrap items-center justify-between gap-3 bg-white">
            <div className="flex flex-wrap items-center gap-4 text-xs text-slate-600">
              <span className="flex items-center gap-1.5">
                <span className="w-3 h-3 rounded-xs bg-rose-50 border border-rose-500 inline-block" />
                <span>Viền đỏ = Ô lỗi định dạng</span>
              </span>
              <span className="flex items-center gap-1.5">
                <span className="w-3 h-3 rounded-xs bg-white border border-slate-300 inline-block" />
                <span>Ô trắng = Dữ liệu hợp lệ</span>
              </span>
              <span className="text-slate-300">|</span>
              <span className="font-semibold text-slate-800">
                Tổng cộng: {rows.length} dòng sản phẩm
              </span>
            </div>

            <div className="flex items-center gap-2.5 relative">
              <div className="relative">
                <button
                  type="button"
                  onClick={() => setShowColumnDropdown(!showColumnDropdown)}
                  className="px-3 py-1.5 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 flex items-center gap-1.5 cursor-pointer"
                >
                  <span>Ẩn/Hiện cột</span>
                  <span className="text-[10px]">▼</span>
                </button>

                {showColumnDropdown && (
                  <div className="absolute right-0 mt-1 w-48 bg-white border border-slate-200 rounded-lg shadow-lg py-2 z-20 text-sm">
                    <div className="px-3 py-1 text-xs font-bold text-slate-400 uppercase tracking-wider">
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
                      <span>Hoàn thiện / Màu</span>
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
                      <span>Tiêu chuẩn FSC</span>
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
                      <span>Bản vẽ kỹ thuật</span>
                    </label>
                  </div>
                )}
              </div>

              <button
                type="button"
                onClick={handleAddRow}
                className="px-3 py-1.5 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 flex items-center gap-1.5 cursor-pointer"
              >
                <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" strokeWidth={2.5} viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
                </svg>
                <span>Thêm dòng</span>
              </button>
            </div>
          </div>

          <div className="overflow-x-auto bg-white">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                  <th className="py-3.5 px-3 w-10 text-center">#</th>
                  <th className="py-3.5 px-3 w-44">Mã SKU nội bộ *</th>
                  <th className="py-3.5 px-3 min-w-[200px]">Mô tả sản phẩm *</th>
                  <th className="py-3.5 px-3 w-48">Kích thước (mm) *</th>
                  {visibleColumns.finishColor && (
                    <th className="py-3.5 px-3 w-40">Hoàn thiện / Màu</th>
                  )}
                  <th className="py-3.5 px-3 w-32">Số lượng đặt *</th>
                  <th className="py-3.5 px-3 w-40">ETD mục tiêu *</th>
                  {visibleColumns.fscRequired && (
                    <th className="py-3.5 px-3 w-32">Tiêu chuẩn FSC</th>
                  )}
                  {visibleColumns.technicalDrawing && (
                    <th className="py-3.5 px-4 min-w-[160px]">Bản vẽ kỹ thuật</th>
                  )}
                  <th className="py-3.5 px-3 w-14 text-center">Xóa</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-sm bg-white">
                {rows.map((row, idx) => (
                  <tr key={idx} className="hover:bg-slate-50/80">
                    <td className="py-3 px-3 text-slate-500 font-semibold text-center">
                      {idx + 1}
                    </td>

                    <td className="py-2.5 px-3">
                      <input
                        type="text"
                        value={row.productSku || ''}
                        placeholder="Mã SKU"
                        onChange={(e) => handleCellChange(idx, 'productSku', e.target.value)}
                        className={`w-full px-3 py-1.5 text-sm rounded-lg border focus:outline-none focus:ring-2 focus:ring-blue-600 ${
                          hasCellError(row, 'productSku')
                            ? 'bg-rose-50 border-rose-500 text-rose-800 font-semibold'
                            : 'bg-white border-slate-300 text-slate-800'
                        }`}
                      />
                    </td>

                    <td className="py-2.5 px-3">
                      <input
                        type="text"
                        value={row.productDescription || ''}
                        placeholder="Tên / Mô tả sản phẩm"
                        onChange={(e) =>
                          handleCellChange(idx, 'productDescription', e.target.value)
                        }
                        className={`w-full px-3 py-1.5 text-sm rounded-lg border focus:outline-none focus:ring-2 focus:ring-blue-600 ${
                          hasCellError(row, 'productDescription')
                            ? 'bg-rose-50 border-rose-500 text-rose-800 font-semibold'
                            : 'bg-white border-slate-300 text-slate-800'
                        }`}
                      />
                    </td>

                    <td className="py-2.5 px-3">
                      <input
                        type="text"
                        value={row.dimensionsMm || ''}
                        placeholder="1600 x 450 x 800"
                        title="Định dạng Dài x Rộng x Cao (ví dụ: 1600 x 450 x 800)"
                        onChange={(e) => handleCellChange(idx, 'dimensionsMm', e.target.value)}
                        className={`w-full px-3 py-1.5 text-sm rounded-lg border focus:outline-none focus:ring-2 focus:ring-blue-600 ${
                          hasCellError(row, 'dimensionsMm')
                            ? 'bg-rose-50 border-rose-500 text-rose-800 font-semibold'
                            : 'bg-white border-slate-300 text-slate-800'
                        }`}
                      />
                    </td>

                    {visibleColumns.finishColor && (
                      <td className="py-2.5 px-3">
                        <input
                          type="text"
                          value={row.finishColor || ''}
                          placeholder="Màu sơn / Hoàn thiện"
                          onChange={(e) => handleCellChange(idx, 'finishColor', e.target.value)}
                          className={`w-full px-3 py-1.5 text-sm rounded-lg border focus:outline-none focus:ring-2 focus:ring-blue-600 ${
                            hasCellError(row, 'finishColor')
                              ? 'bg-rose-50 border-rose-500 text-rose-800 font-semibold'
                              : 'bg-white border-slate-300 text-slate-800'
                          }`}
                        />
                      </td>
                    )}

                    <td className="py-2.5 px-3">
                      <input
                        type="number"
                        min="1"
                        value={row.orderQty ?? ''}
                        onChange={(e) => handleCellChange(idx, 'orderQty', e.target.value)}
                        className={`w-full px-3 py-1.5 text-sm rounded-lg border focus:outline-none focus:ring-2 focus:ring-blue-600 ${
                          hasCellError(row, 'orderQty')
                            ? 'bg-rose-50 border-rose-500 text-rose-800 font-semibold'
                            : 'bg-white border-slate-300 text-slate-800 font-semibold'
                        }`}
                      />
                    </td>

                    <td className="py-2.5 px-3">
                      <input
                        type="date"
                        value={row.targetEtd ? String(row.targetEtd).split('T')[0] : ''}
                        onChange={(e) => handleCellChange(idx, 'targetEtd', e.target.value)}
                        className={`w-full px-3 py-1.5 text-sm rounded-lg border focus:outline-none focus:ring-2 focus:ring-blue-600 ${
                          hasCellError(row, 'targetEtd')
                            ? 'bg-rose-50 border-rose-500 text-rose-800 font-semibold'
                            : 'bg-white border-slate-300 text-slate-800'
                        }`}
                      />
                    </td>

                    {visibleColumns.fscRequired && (
                      <td className="py-2.5 px-3">
                        <select
                          value={row.fscRequired ? 'true' : 'false'}
                          onChange={(e) =>
                            handleCellChange(idx, 'fscRequired', e.target.value === 'true')
                          }
                          className="w-full px-2.5 py-1.5 text-sm rounded-lg border border-slate-300 bg-white font-medium focus:outline-none focus:ring-2 focus:ring-blue-600"
                        >
                          <option value="true">FSC 100%</option>
                          <option value="false">Không</option>
                        </select>
                      </td>
                    )}

                    {visibleColumns.technicalDrawing && (
                      <td className="py-2.5 px-4">
                        <input
                          type="text"
                          value={row.technicalDrawingName || ''}
                          placeholder="CAD_Drawing.pdf"
                          onChange={(e) =>
                            handleCellChange(idx, 'technicalDrawingName', e.target.value)
                          }
                          className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-300 bg-white text-slate-800 font-mono focus:outline-none focus:ring-2 focus:ring-blue-600"
                        />
                      </td>
                    )}

                    <td className="py-2.5 px-3 text-center">
                      <button
                        type="button"
                        onClick={() => handleDeleteRowAt(idx)}
                        title="Xóa dòng này"
                        className="text-slate-400 hover:text-rose-600 transition p-1.5 cursor-pointer"
                      >
                        <svg className="w-4 h-4 mx-auto" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                        </svg>
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
