import { useEffect, useState } from "react";
import { catalogApi } from "../../catalog/api/catalogApi";
import { DEFAULT_MASTER_MATERIALS } from "../../catalog/data/defaultMaterials";
import type { MaterialCatalogDto } from "../../catalog/types";
import type { BomDetailDto } from "../types";

interface MaterialItemModalProps {
  isOpen: boolean;
  editingItem: BomDetailDto | null;
  onClose: () => void;
  onSave: (item: BomDetailDto) => void;
}

const CATEGORIES = ["LUMBER", "HARDWARE", "FINISH_PAINT", "PACKAGING"];

export default function MaterialItemModal({
  isOpen,
  editingItem,
  onClose,
  onSave,
}: MaterialItemModalProps) {
  const [masterMaterials, setMasterMaterials] = useState<MaterialCatalogDto[]>(
    DEFAULT_MASTER_MATERIALS,
  );
  const [selectedMaterialId, setSelectedMaterialId] = useState<number>(
    DEFAULT_MASTER_MATERIALS[0]?.materialId || 1,
  );
  const [materialCode, setMaterialCode] = useState("");
  const [materialGroup, setMaterialGroup] = useState("LUMBER");
  const [materialName, setMaterialName] = useState("");
  const [dimensionSpec, setDimensionSpec] = useState("");
  const [unitOfMeasure, setUnitOfMeasure] = useState("M3");
  const [fscCertified, setFscCertified] = useState(true);
  const [netQtyPerUnit, setNetQtyPerUnit] = useState("0.018");
  const [notes, setNotes] = useState("");
  const [errorMsg, setErrorMsg] = useState("");

  useEffect(() => {
    if (!isOpen) return;
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
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) return;
    if (editingItem) {
      const matched =
        masterMaterials.find(
          (m) =>
            m.materialId === editingItem.materialId ||
            m.materialCode.toUpperCase() === editingItem.materialCode.toUpperCase(),
        ) || masterMaterials[0];
      setSelectedMaterialId(editingItem.materialId || matched?.materialId || 1);
      setMaterialCode(editingItem.materialCode);
      setMaterialGroup(editingItem.materialGroup || matched?.materialGroup || "LUMBER");
      setMaterialName(editingItem.materialName);
      setDimensionSpec(editingItem.dimensionSpec || matched?.dimensionSpec || "");
      setUnitOfMeasure(editingItem.unitOfMeasure || matched?.unitOfMeasure || "M3");
      setFscCertified(editingItem.fscCertified ?? matched?.fscCertified ?? false);
      setNetQtyPerUnit(String(editingItem.netQtyPerUnit));
      setNotes(editingItem.notes || "");
    } else {
      const first = masterMaterials[0] || DEFAULT_MASTER_MATERIALS[0];
      if (first) {
        setSelectedMaterialId(first.materialId);
        setMaterialCode(first.materialCode);
        setMaterialGroup(first.materialGroup);
        setMaterialName(first.materialName);
        setDimensionSpec(first.dimensionSpec);
        setUnitOfMeasure(first.unitOfMeasure);
        setFscCertified(first.fscCertified);
      }
      setNetQtyPerUnit("0.025");
      setNotes("");
    }
    setErrorMsg("");
  }, [isOpen, editingItem, masterMaterials]);

  if (!isOpen) return null;

  const handleSelectMasterMaterial = (matIdStr: string) => {
    const id = Number(matIdStr);
    setSelectedMaterialId(id);
    const mat = masterMaterials.find((m) => m.materialId === id);
    if (mat) {
      setMaterialCode(mat.materialCode);
      setMaterialGroup(mat.materialGroup);
      setMaterialName(mat.materialName);
      setDimensionSpec(mat.dimensionSpec);
      setUnitOfMeasure(mat.unitOfMeasure);
      setFscCertified(mat.fscCertified);
      setErrorMsg("");
    }
  };

  const handleCodeChange = (val: string) => {
    setMaterialCode(val);
    const matched = masterMaterials.find(
      (m) => m.materialCode.toUpperCase() === val.trim().toUpperCase(),
    );
    if (matched) {
      setSelectedMaterialId(matched.materialId);
      setMaterialGroup(matched.materialGroup);
      setMaterialName(matched.materialName);
      setDimensionSpec(matched.dimensionSpec);
      setUnitOfMeasure(matched.unitOfMeasure);
      setFscCertified(matched.fscCertified);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const parsedQty = Number(netQtyPerUnit);
    if (!materialCode.trim() || !materialName.trim()) {
      setErrorMsg("Bắt buộc nhập mã vật tư và tên vật tư.");
      return;
    }
    if (Number.isNaN(parsedQty) || parsedQty <= 0) {
      setErrorMsg("Định mức thực trên mỗi sản phẩm phải lớn hơn 0.");
      return;
    }

    const matchedByCode = masterMaterials.find(
      (m) => m.materialCode.toUpperCase() === materialCode.trim().toUpperCase(),
    );
    const resolvedMatId =
      matchedByCode?.materialId ||
      selectedMaterialId ||
      editingItem?.materialId ||
      masterMaterials[0]?.materialId ||
      1;

    onSave({
      bomDetailId: editingItem?.bomDetailId || Date.now(),
      materialId: resolvedMatId,
      materialCode: materialCode.trim().toUpperCase(),
      materialGroup,
      materialName: materialName.trim(),
      dimensionSpec: dimensionSpec.trim() || null,
      fscCertified,
      unitOfMeasure: unitOfMeasure.trim() || "PCS",
      netQtyPerUnit: parsedQty,
      grossTotalDemand: editingItem?.grossTotalDemand || 0,
      notes: notes.trim() || null,
    });
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
          <div>
            <h2 className="text-sm font-extrabold text-slate-900">
              {editingItem ? "Sửa dòng vật tư trong BOM" : "Thêm dòng vật tư thô"}
            </h2>
            <p className="text-[11px] text-slate-500 mt-0.5">
              Chọn từ danh mục vật tư gốc của xưởng hoặc nhập mã vật tư đã đăng ký.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1.5 py-0.5 rounded"
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          {errorMsg && (
            <div className="p-2.5 rounded bg-rose-50 border border-rose-200 text-rose-700 text-xs font-medium">
              {errorMsg}
            </div>
          )}

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Tra cứu danh mục vật tư gốc (đồng bộ CSDL)
            </label>
            <select
              value={selectedMaterialId}
              onChange={(e) => handleSelectMasterMaterial(e.target.value)}
              className="w-full px-3 py-2 text-xs bg-amber-50/40 border border-amber-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 font-semibold text-slate-800"
            >
              {masterMaterials.map((mat) => (
                <option key={mat.materialId} value={mat.materialId}>
                  [{mat.materialCode}] {mat.materialName} ({mat.unitOfMeasure} - {mat.materialGroup})
                </option>
              ))}
            </select>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Mã vật tư
              </label>
              <input
                type="text"
                value={materialCode}
                onChange={(e) => handleCodeChange(e.target.value)}
                className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 font-mono"
                placeholder="VD: WOOD_OAK_01"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Nhóm vật tư
              </label>
              <select
                value={materialGroup}
                onChange={(e) => setMaterialGroup(e.target.value)}
                className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                {CATEGORIES.map((cat) => (
                  <option key={cat} value={cat}>
                    {cat}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Tên vật tư
            </label>
            <input
              type="text"
              value={materialName}
              onChange={(e) => setMaterialName(e.target.value)}
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              placeholder="e.g. Gỗ sồi Bắc Mỹ xẻ sấy FAS (Oak)"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Quy cách kích thước
              </label>
              <input
                type="text"
                value={dimensionSpec}
                onChange={(e) => setDimensionSpec(e.target.value)}
                className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="VD: 26mm x 150mm x 2400mm"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Đơn vị tính (ĐVT)
              </label>
              <input
                type="text"
                value={unitOfMeasure}
                onChange={(e) => setUnitOfMeasure(e.target.value)}
                className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="M3, CÁI, LÍT"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Định mức thực trên mỗi sản phẩm
              </label>
              <input
                type="number"
                step="any"
                min="0.0001"
                value={netQtyPerUnit}
                onChange={(e) => setNetQtyPerUnit(e.target.value)}
                className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 font-bold"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Ghi chú kỹ thuật (không bắt buộc)
              </label>
              <input
                type="text"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
                placeholder="VD: Khung chính / Sơn phủ"
              />
            </div>
          </div>

          <div className="pt-2 flex items-center justify-end gap-2.5">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md transition"
            >
              Hủy
            </button>
            <button
              type="submit"
              className="px-4 py-2 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition"
            >
              {editingItem ? "Lưu thay đổi" : "Thêm vật tư"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
