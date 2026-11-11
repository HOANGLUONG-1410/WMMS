import { useEffect, useRef, useState } from "react";
import { isAxiosError } from "axios";
import { catalogApi } from "../../catalog/api/catalogApi";
import { DEFAULT_MASTER_MATERIALS } from "../../catalog/data/defaultMaterials";
import type { MaterialCatalogDto } from "../../catalog/types";
import { bomApi } from "../api/bomApi";
import { useConfirmBomStaging, usePreviewBomStaging } from "../hooks/useBom";
import type { BomDto, BomStagingRowDto } from "../types";

interface BomStagingModalProps {
  isOpen: boolean;
  orderItemId: number;
  productId: number;
  productSku: string;
  productName: string;
  existingBomId?: number | null;
  initialScrapRate: number;
  onClose: () => void;
  onConfirmedBom: (bom: BomDto, message: string) => void;
}

export default function BomStagingModal({
  isOpen,
  orderItemId,
  productId,
  productSku,
  productName,
  existingBomId,
  initialScrapRate,
  onClose,
  onConfirmedBom,
}: BomStagingModalProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const previewMutation = usePreviewBomStaging();
  const confirmMutation = useConfirmBomStaging();

  const [masterMaterials, setMasterMaterials] = useState<MaterialCatalogDto[]>(
    DEFAULT_MASTER_MATERIALS,
  );
  const [selectedFileName, setSelectedFileName] = useState<string>("");
  const [sheetMeta, setSheetMeta] = useState<{
    sheetName?: string;
    detectedProductSku?: string | null;
    detectedProductDimensions?: string | null;
  } | null>(null);
  const [scrapRate, setScrapRate] = useState<number>(initialScrapRate || 3.5);
  const [rows, setRows] = useState<BomStagingRowDto[]>([]);
  const [serverBanner, setServerBanner] = useState<{
    type: "info" | "success" | "error";
    text: string;
  } | null>(null);

  useEffect(() => {
    if (!isOpen) return;
    setScrapRate(initialScrapRate >= 3 && initialScrapRate <= 5 ? initialScrapRate : 3.5);
    setServerBanner(null);

    catalogApi
      .getMaterials()
      .then((list) => {
        if (list && list.length > 0) {
          setMasterMaterials(list);
        }
      })
      .catch(() => {
        setMasterMaterials(DEFAULT_MASTER_MATERIALS);
      });
  }, [isOpen, initialScrapRate]);

  if (!isOpen) return null;

  // Kiểm tra tính hợp lệ của 1 dòng theo đúng logic của BomRoutingService.PreviewBomExcelStagingAsync
  const revalidateRowLocally = (row: BomStagingRowDto): BomStagingRowDto => {
    const errors: string[] = [];
    const codeUpper = (row.materialCode || "").trim().toUpperCase();
    const matchedMat = masterMaterials.find(
      (m) => m.materialCode.toUpperCase() === codeUpper,
    );

    let resolvedMaterialId = row.resolvedMaterialId ?? null;
    let materialName = row.materialName;
    let materialGroup = row.materialGroup;
    let unitOfMeasure = row.unitOfMeasure;
    let availableStock = row.availableStock ?? 0;

    if (!codeUpper) {
      errors.push("Mã nguyên vật liệu (MaterialCode) không được để trống.");
      resolvedMaterialId = null;
    } else if (matchedMat) {
      resolvedMaterialId = matchedMat.materialId;
      materialName = matchedMat.materialName;
      materialGroup = matchedMat.materialGroup;
      unitOfMeasure = matchedMat.unitOfMeasure;
      availableStock = matchedMat.currentStockQty ?? availableStock;
    } else if (!resolvedMaterialId) {
      errors.push(`Mã nguyên vật liệu '${row.materialCode}' không tồn tại trong danh mục Master Data.`);
    }

    if (Number.isNaN(Number(row.netQtyPerUnit)) || Number(row.netQtyPerUnit) <= 0) {
      errors.push("Định mức 1 sản phẩm (NetQtyPerUnit) phải lớn hơn 0.");
    }

    return {
      ...row,
      materialCode: codeUpper,
      materialName,
      materialGroup,
      unitOfMeasure,
      availableStock,
      resolvedMaterialId,
      isValid: errors.length === 0,
      errors,
    };
  };

  // Upload file Excel lên POST /api/v1/bom/staging/preview
  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setSelectedFileName(file.name);
    setServerBanner(null);

    try {
      const res = await previewMutation.mutateAsync({
        file,
        orderItemId,
        productId,
      });

      if (res?.data) {
        const incomingRows = (res.data.rows || []).map((r, idx) => ({
          ...r,
          rowIndex: r.rowIndex ?? r.rowNumber ?? idx + 1,
        }));
        setRows(incomingRows);
        setSheetMeta({
          sheetName: res.data.sheetName,
          detectedProductSku: res.data.detectedProductSku,
          detectedProductDimensions: res.data.detectedProductDimensions,
        });

        const errCount = res.data.errorRows ?? incomingRows.filter((r) => !r.isValid).length;
        const isReady = !res.data.hasErrors && errCount === 0 && incomingRows.length > 0;

        setServerBanner({
          type: isReady ? "success" : "error",
          text:
            res.message ||
            `Đã đọc ${res.data.totalRows} dòng từ sheet "${res.data.sheetName || "Sheet1"}": ${res.data.validRows} hợp lệ, ${errCount} dòng lỗi.`,
        });
      }
    } catch (err) {
      let msg = "Không thể phân tích file Excel BOM trên máy chủ.";
      if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setServerBanner({ type: "error", text: msg });
    } finally {
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
    }
  };

  // Thêm 1 dòng mới trực tiếp trên bảng Staging nếu người dùng muốn bổ sung
  const handleAddBlankStagingRow = () => {
    const defaultMat = masterMaterials[0] || DEFAULT_MASTER_MATERIALS[0];
    const nextRowIndex =
      rows.length > 0
        ? Math.max(...rows.map((r, i) => r.rowIndex ?? r.rowNumber ?? i + 1)) + 1
        : 1;
    const newRow: BomStagingRowDto = revalidateRowLocally({
      rowIndex: nextRowIndex,
      materialCode: defaultMat.materialCode,
      materialName: defaultMat.materialName,
      materialGroup: defaultMat.materialGroup,
      unitOfMeasure: defaultMat.unitOfMeasure,
      netQtyPerUnit: 0.015,
      subAssemblyName: "",
      componentPartName: "",
      componentDimensionSpec: "",
      availableStock: defaultMat.currentStockQty ?? 0,
      notes: "",
      resolvedMaterialId: defaultMat.materialId,
      isValid: true,
      errors: [],
    });
    setRows((prev) => [...prev, newRow]);
  };

  const handleUpdateRowField = (
    index: number,
    field: keyof BomStagingRowDto,
    value: string | number,
  ) => {
    setRows((prev) =>
      prev.map((r, idx) => {
        if (idx !== index) return r;
        const updated = { ...r, [field]: value };
        if (field === "materialCode") {
          return revalidateRowLocally({ ...updated, resolvedMaterialId: null });
        }
        return revalidateRowLocally(updated);
      }),
    );
  };

  const handleRemoveRow = (index: number) => {
    setRows((prev) => prev.filter((_, idx) => idx !== index));
  };

  const handleClearAllRows = () => {
    setRows([]);
    setSheetMeta(null);
    setSelectedFileName("");
    setServerBanner(null);
  };

  const totalRows = rows.length;
  const validRows = rows.filter((r) => r.isValid).length;
  const errorRows = totalRows - validRows;
  const isReadyToConfirm = totalRows > 0 && errorRows === 0;

  // Xác nhận lưu BOM từ bảng Staging (POST /api/v1/bom/staging/confirm)
  const handleConfirmStaging = async () => {
    if (rows.length === 0) {
      setServerBanner({
        type: "error",
        text: "Bảng BOM Staging chưa có dòng dữ liệu nào. Vui lòng tải file Excel hoặc thêm dòng vật tư.",
      });
      return;
    }

    if (errorRows > 0) {
      setServerBanner({
        type: "error",
        text: `Còn ${errorRows} dòng lỗi trên bảng Staging. Vui lòng sửa Mã NVL / Định mức > 0 hoặc xóa dòng lỗi trước khi xác nhận.`,
      });
      return;
    }

    setServerBanner(null);

    try {
      const res = await confirmMutation.mutateAsync({
        orderItemId,
        productId,
        scrapRatePercent: scrapRate,
        rows,
      });

      if (res?.data) {
        onConfirmedBom(
          res.data,
          res.message || "Xác nhận dữ liệu BOM Staging và khởi tạo BOM thành công!",
        );
        onClose();
        return;
      }
    } catch (err) {
      // Nếu OrderItem đã có sẵn bản nháp BOM (Backend trả 409 Conflict theo BR-001),
      // tự động gọi PUT /api/v1/bom/{existingBomId} để cập nhật các dòng Staging (kèm SubAssembly & ComponentDimensionSpec) vào BOM đó
      if (isAxiosError(err) && err.response?.status === 409 && existingBomId && existingBomId > 0) {
        try {
          const updateRes = await bomApi.updateBom(existingBomId, {
            scrapRatePercent: scrapRate,
            details: rows.map((r) => ({
              materialId: r.resolvedMaterialId || 1,
              netQtyPerUnit: Number(r.netQtyPerUnit),
              subAssemblyName: r.subAssemblyName || null,
              componentDimensionSpec: r.componentDimensionSpec || null,
              notes: r.notes || null,
            })),
          });
          if (updateRes?.data) {
            onConfirmedBom(
              updateRes.data,
              updateRes.message ||
                "Đã cập nhật dữ liệu từ BOM Staging vào BOM hiện tại thành công!",
            );
            onClose();
            return;
          }
        } catch (updateErr) {
          let updateMsg = "Lỗi khi cập nhật BOM từ dữ liệu Staging.";
          if (isAxiosError(updateErr)) {
            updateMsg = updateErr.response?.data?.message || updateErr.message || updateMsg;
          }
          setServerBanner({ type: "error", text: updateMsg });
          return;
        }
      }

      let msg = "Xác nhận BOM Staging thất bại.";
      if (isAxiosError(err)) {
        msg = err.response?.data?.message || err.message || msg;
      }
      setServerBanner({ type: "error", text: msg });
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-6xl overflow-hidden flex flex-col max-h-[92vh]">
        {/* Header */}
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between bg-white">
          <div>
            <div className="flex items-center gap-2.5">
              <span className="px-2.5 py-0.5 text-xs font-bold uppercase rounded bg-blue-50 text-blue-700 border border-blue-200">
                STAGING BOM
              </span>
              <h2 className="text-base font-bold text-slate-900">
                Nhập &amp; kiểm tra dữ liệu BOM Excel (.xlsx / .xls)
              </h2>
            </div>
            <p className="text-xs text-slate-500 mt-1">
              Dòng hàng mục tiêu #{orderItemId} • SKU:{" "}
              <strong className="font-mono text-slate-800">{productSku}</strong> ({productName})
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-xl font-bold leading-none px-2 py-1 rounded cursor-pointer"
          >
            ×
          </button>
        </div>

        {/* Body */}
        <div className="p-6 space-y-4 overflow-y-auto flex-1">
          {/* Hướng dẫn cấu trúc cột Excel */}
          <div className="bg-blue-50/60 border border-blue-200 rounded-xl p-4 flex flex-col lg:flex-row lg:items-center justify-between gap-4">
            <div className="space-y-1.5">
              <div className="text-sm font-bold text-blue-900">
                Hỗ trợ 2 định dạng Excel BOM:
              </div>
              <div className="text-xs text-blue-800 space-y-1">
                <div>
                  1. <strong>Sổ bóc tách phôi xưởng (.xls / .xlsx)</strong>: Tự động tách cụm BTP, tên chi tiết và quy đổi <code className="font-mono">Dài x Bản x Dầy</code> sang ĐVT tồn kho.
                </div>
                <div>
                  2. <strong>Bảng BOM tiêu chuẩn</strong>:{" "}
                  <code className="font-mono bg-white px-1.5 py-0.5 rounded border border-blue-200">
                    Mã NVL / Material Code
                  </code>{" "}
                  •{" "}
                  <code className="font-mono bg-white px-1.5 py-0.5 rounded border border-blue-200">
                    Tên NVL / Material Name
                  </code>{" "}
                  •{" "}
                  <code className="font-mono bg-white px-1.5 py-0.5 rounded border border-blue-200">
                    ĐVT / Unit
                  </code>{" "}
                  •{" "}
                  <code className="font-mono bg-white px-1.5 py-0.5 rounded border border-blue-200">
                    Định mức / NetQtyPerUnit
                  </code>
                </div>
              </div>
            </div>

            <div className="flex flex-wrap items-center gap-2.5 shrink-0">
              <input
                ref={fileInputRef}
                type="file"
                accept=".xlsx,.xls"
                onChange={handleFileChange}
                className="hidden"
              />
              <button
                type="button"
                onClick={() => fileInputRef.current?.click()}
                disabled={previewMutation.isPending}
                className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition disabled:opacity-60 cursor-pointer"
              >
                {previewMutation.isPending
                  ? "Đang phân tích..."
                  : "Chọn file Excel (.xlsx / .xls)"}
              </button>
              <button
                type="button"
                onClick={handleAddBlankStagingRow}
                className="px-3.5 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg transition cursor-pointer"
              >
                Thêm dòng thủ công
              </button>
              {rows.length > 0 && (
                <button
                  type="button"
                  onClick={handleClearAllRows}
                  className="px-3.5 py-2 bg-white hover:bg-rose-50 text-rose-600 border border-rose-200 text-sm font-semibold rounded-lg transition cursor-pointer"
                >
                  Xóa trắng bảng
                </button>
              )}
            </div>
          </div>

          {/* Thông báo từ Server */}
          {serverBanner && (
            <div
              className={`px-4 py-3 rounded-lg border text-sm font-medium flex items-center justify-between ${
                serverBanner.type === "success"
                  ? "bg-emerald-50 border-emerald-200 text-emerald-800"
                  : serverBanner.type === "error"
                    ? "bg-rose-50 border-rose-200 text-rose-700"
                    : "bg-blue-50 border-blue-200 text-blue-800"
              }`}
            >
              <span>{serverBanner.text}</span>
              <button
                type="button"
                onClick={() => setServerBanner(null)}
                className="text-slate-400 hover:text-slate-600 ml-3 font-bold cursor-pointer"
              >
                ×
              </button>
            </div>
          )}

          {/* Thanh KPI thống kê Staging & Scrap Rate */}
          <div className="flex flex-wrap items-center justify-between gap-3 bg-[#F8FAFC] border border-slate-200 rounded-xl px-4 py-3">
            <div className="flex flex-wrap items-center gap-3 text-sm">
              {selectedFileName && (
                <span className="font-semibold text-slate-700">
                  Tệp: <code className="font-mono text-blue-700">{selectedFileName}</code>
                </span>
              )}
              {sheetMeta?.sheetName && (
                <span className="text-slate-600">
                  Trang tính: <strong className="text-slate-800">{sheetMeta.sheetName}</strong>
                </span>
              )}
              {sheetMeta?.detectedProductSku && (
                <span className="px-2.5 py-0.5 rounded bg-blue-50 text-blue-700 border border-blue-200 font-mono text-xs font-bold">
                  Mã SP phát hiện: {sheetMeta.detectedProductSku}
                  {sheetMeta.detectedProductDimensions
                    ? ` (${sheetMeta.detectedProductDimensions})`
                    : ""}
                </span>
              )}
              <span className="text-slate-600">
                Tổng dòng: <strong className="text-slate-900">{totalRows}</strong>
              </span>
              <span className="text-emerald-700">
                Hợp lệ: <strong>{validRows}</strong>
              </span>
              <span className="text-rose-600">
                Lỗi: <strong>{errorRows}</strong>
              </span>
              <span
                className={`px-2.5 py-0.5 text-xs font-bold uppercase rounded border ${
                  isReadyToConfirm
                    ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                    : "bg-amber-50 text-amber-800 border-amber-200"
                }`}
              >
                {isReadyToConfirm ? "SẴN SÀNG XÁC NHẬN" : "CHỜ KIỂM TRA"}
              </span>
            </div>

            <div className="flex items-center gap-2">
              <label className="text-sm font-semibold text-slate-700">
                Tỷ lệ hao hụt % (3,0% - 5,0%):
              </label>
              <input
                type="number"
                step="0.1"
                min="3"
                max="5"
                value={scrapRate}
                onChange={(e) => setScrapRate(parseFloat(e.target.value) || 3.0)}
                className="w-20 px-2.5 py-1.5 text-sm font-bold text-slate-900 bg-white border border-slate-300 rounded-lg text-center"
              />
              <span className="text-sm text-slate-500">%</span>
            </div>
          </div>

          {/* Bảng BOM Staging Grid */}
          <div className="border border-slate-200 rounded-xl overflow-x-auto bg-white">
            <table className="w-full text-left border-collapse text-sm">
              <thead>
                <tr className="bg-[#F8FAFC] border-b border-slate-200 text-xs uppercase tracking-wider font-bold text-slate-500">
                  <th className="py-3 px-3 w-12">Dòng</th>
                  <th className="py-3 px-3 w-48">Cụm BTP / Chi tiết phôi</th>
                  <th className="py-3 px-3 w-52">Mã NVL</th>
                  <th className="py-3 px-3">Tên nguyên vật liệu &amp; Ghi chú</th>
                  <th className="py-3 px-2.5 w-28">Nhóm</th>
                  <th className="py-3 px-2.5 w-20 text-center">ĐVT</th>
                  <th className="py-3 px-3 w-28 text-right">Định mức / SP</th>
                  <th className="py-3 px-3 w-52">Đối chiếu DB &amp; Tồn kho</th>
                  <th className="py-3 px-2.5 w-14 text-right">Xóa</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {rows.length === 0 ? (
                  <tr>
                    <td colSpan={9} className="py-12 text-center text-slate-500 text-sm">
                      Chưa có dữ liệu Staging. Nhấn{" "}
                      <strong>Chọn file Excel (.xlsx / .xls)</strong> để tải lên hoặc nhấn{" "}
                      <strong>Thêm dòng thủ công</strong>.
                    </td>
                  </tr>
                ) : (
                  rows.map((row, idx) => {
                    const displayRowNum = row.rowIndex ?? row.rowNumber ?? idx + 1;
                    return (
                      <tr
                        key={`${displayRowNum}-${idx}`}
                        className={row.isValid ? "bg-white hover:bg-slate-50" : "bg-rose-50/60"}
                      >
                        <td className="py-3 px-3 font-mono text-slate-500 font-semibold">
                          #{displayRowNum}
                        </td>

                        <td className="py-3 px-3">
                          <div className="space-y-1.5">
                            <input
                              type="text"
                              value={row.subAssemblyName || ""}
                              onChange={(e) =>
                                handleUpdateRowField(idx, "subAssemblyName", e.target.value)
                              }
                              placeholder="Cụm BTP (VD: phụ kiện sồi)"
                              className="w-full px-2.5 py-1 text-xs font-semibold border border-slate-300 rounded-md bg-white text-slate-800"
                            />
                            <input
                              type="text"
                              value={row.componentDimensionSpec || row.componentPartName || ""}
                              onChange={(e) =>
                                handleUpdateRowField(idx, "componentDimensionSpec", e.target.value)
                              }
                              placeholder="Quy cách phôi (Dài x Bản x Dầy)"
                              className="w-full px-2.5 py-1 text-xs font-mono border border-slate-200 rounded-md bg-slate-50 text-slate-600"
                            />
                          </div>
                        </td>

                        <td className="py-3 px-3">
                          <div className="space-y-1.5">
                            <input
                              type="text"
                              value={row.materialCode}
                              onChange={(e) =>
                                handleUpdateRowField(idx, "materialCode", e.target.value)
                              }
                              className={`w-full px-2.5 py-1 text-sm font-mono font-bold rounded-md border ${
                                row.isValid
                                  ? "border-slate-300 bg-white text-slate-900"
                                  : "border-rose-400 bg-white text-rose-800"
                              }`}
                            />
                            <select
                              value={row.materialCode.toUpperCase()}
                              onChange={(e) =>
                                handleUpdateRowField(idx, "materialCode", e.target.value)
                              }
                              className="w-full px-2 py-1 text-xs bg-slate-50 border border-slate-200 rounded-md text-slate-600"
                            >
                              <option value="">-- Đổi mã vật tư chuẩn DB --</option>
                              {masterMaterials.map((m) => (
                                <option key={m.materialId} value={m.materialCode}>
                                  {m.materialCode} ({m.materialName})
                                </option>
                              ))}
                            </select>
                          </div>
                        </td>

                        <td className="py-3 px-3">
                          <div className="space-y-1.5">
                            <input
                              type="text"
                              value={row.materialName}
                              onChange={(e) =>
                                handleUpdateRowField(idx, "materialName", e.target.value)
                              }
                              className="w-full px-2.5 py-1 text-sm font-medium border border-slate-300 rounded-md bg-white text-slate-800"
                            />
                            <input
                              type="text"
                              value={row.notes || ""}
                              onChange={(e) => handleUpdateRowField(idx, "notes", e.target.value)}
                              placeholder="Ghi chú chi tiết..."
                              className="w-full px-2.5 py-1 text-xs border border-slate-200 rounded-md bg-slate-50 text-slate-600"
                            />
                          </div>
                        </td>

                        <td className="py-3 px-2.5">
                          <input
                            type="text"
                            value={row.materialGroup}
                            onChange={(e) =>
                              handleUpdateRowField(idx, "materialGroup", e.target.value)
                            }
                            className="w-full px-2 py-1 text-xs font-bold border border-slate-300 rounded-md bg-white text-slate-700"
                          />
                        </td>

                        <td className="py-3 px-2.5">
                          <input
                            type="text"
                            value={row.unitOfMeasure}
                            onChange={(e) =>
                              handleUpdateRowField(idx, "unitOfMeasure", e.target.value)
                            }
                            className="w-full px-2 py-1 text-xs border border-slate-300 rounded-md bg-white text-slate-700 text-center"
                          />
                        </td>

                        <td className="py-3 px-3">
                          <input
                            type="number"
                            step="any"
                            value={row.netQtyPerUnit}
                            onChange={(e) =>
                              handleUpdateRowField(
                                idx,
                                "netQtyPerUnit",
                                parseFloat(e.target.value) || 0,
                              )
                            }
                            className={`w-full px-2.5 py-1 text-sm font-bold rounded-md border text-right ${
                              row.netQtyPerUnit > 0
                                ? "border-slate-300 bg-white text-slate-900"
                                : "border-rose-400 bg-white text-rose-700"
                            }`}
                          />
                          {row.rawPiecesPerUnit !== undefined && row.rawPiecesPerUnit !== null && (
                            <div className="text-xs text-slate-500 text-right mt-0.5">
                              ({row.rawPiecesPerUnit} thanh/tấm)
                            </div>
                          )}
                        </td>

                        <td className="py-3 px-3">
                          {row.isValid ? (
                            <div className="space-y-1">
                              <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-emerald-50 text-emerald-700 border border-emerald-200 text-xs font-bold">
                                ✓ Khớp DB (#{row.resolvedMaterialId})
                              </span>
                              {row.availableStock !== undefined && (
                                <div className="text-xs text-slate-600 font-medium">
                                  Tồn khả dụng:{" "}
                                  <strong className="text-slate-900">
                                    {row.availableStock} {row.unitOfMeasure}
                                  </strong>
                                </div>
                              )}
                            </div>
                          ) : (
                            <div className="space-y-0.5">
                              {row.errors.map((errText, i) => (
                                <div key={i} className="text-xs font-semibold text-rose-700">
                                  • {errText}
                                </div>
                              ))}
                            </div>
                          )}
                        </td>

                        <td className="py-3 px-2.5 text-right">
                          <button
                            type="button"
                            onClick={() => handleRemoveRow(idx)}
                            className="px-2 py-1 text-rose-600 hover:bg-rose-50 rounded-md text-xs font-bold transition cursor-pointer"
                            title="Xóa dòng này"
                          >
                            ×
                          </button>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-slate-200 bg-[#F8FAFC] flex flex-wrap items-center justify-between gap-3">
          <div />
          <div className="flex items-center gap-2.5">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg transition cursor-pointer"
            >
              Đóng
            </button>
            <button
              type="button"
              onClick={handleConfirmStaging}
              disabled={!isReadyToConfirm || confirmMutation.isPending}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition disabled:opacity-50 cursor-pointer"
            >
              {confirmMutation.isPending
                ? "Đang lưu BOM..."
                : `Xác nhận & Lưu BOM (${validRows} dòng)`}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
