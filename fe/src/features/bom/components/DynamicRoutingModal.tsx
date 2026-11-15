import { useEffect, useLayoutEffect, useRef, useState } from "react";
import type { RoutingStageDto } from "../types";

interface DynamicRoutingModalProps {
  isOpen: boolean;
  stages: RoutingStageDto[];
  onClose: () => void;
  onSave: (updatedStages: RoutingStageDto[]) => void;
}

export default function DynamicRoutingModal({
  isOpen,
  stages,
  onClose,
  onSave,
}: DynamicRoutingModalProps) {
  const [localStages, setLocalStages] = useState<RoutingStageDto[]>(stages);
  const [newStepName, setNewStepName] = useState("");
  const [showAddInput, setShowAddInput] = useState(false);

  // State hỗ trợ Drag & Drop công đoạn
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

  // Refs phục vụ hiệu ứng trượt mượt mà (FLIP Animation)
  const itemRefs = useRef<Map<number, HTMLDivElement>>(new Map());
  const prevPositionsRef = useRef<Map<number, number>>(new Map());

  useEffect(() => {
    if (isOpen) {
      // Đảm bảo mỗi stage có stageId duy nhất và stageSequence chuẩn
      setLocalStages(
        stages.map((s, idx) => ({
          ...s,
          stageId: s.stageId || Date.now() + idx,
          stageSequence: idx + 1,
        })),
      );
      setShowAddInput(false);
      setNewStepName("");
      setDraggedIndex(null);
      prevPositionsRef.current.clear();
    }
  }, [isOpen, stages]);

  // Áp dụng kỹ thuật FLIP (First, Last, Invert, Play) để các công đoạn trượt mượt mà khi nhảy vị trí
  useLayoutEffect(() => {
    if (prevPositionsRef.current.size === 0) return;

    localStages.forEach((stg) => {
      const el = itemRefs.current.get(stg.stageId);
      const prevTop = prevPositionsRef.current.get(stg.stageId);
      if (!el || prevTop === undefined) return;

      const currentTop = el.getBoundingClientRect().top;
      const deltaY = prevTop - currentTop;

      if (deltaY !== 0) {
        // Invert
        el.style.transform = `translateY(${deltaY}px)`;
        el.style.transition = "transform 0s";

        // Play
        requestAnimationFrame(() => {
          el.style.transform = "translateY(0)";
          el.style.transition = "transform 240ms cubic-bezier(0.2, 0, 0, 1)";
        });
      }
    });

    prevPositionsRef.current.clear();
  }, [localStages]);

  if (!isOpen) return null;

  // Làm sạch tiền tố số cũ (ví dụ "1. Pha phôi" hoặc "Step 8: Tốt nghiệp") để tránh trùng "Công đoạn 1: 3. Pha phôi" khi đổi vị trí
  const cleanStageName = (name: string) => {
    return (name || "").replace(/^\s*(Step\s*\d+\s*:\s*|\d+\s*[\.\:\-]\s*)/i, "").trim();
  };

  const handleToggleHoldPoint = (index: number) => {
    setLocalStages((prev) =>
      prev.map((stg, idx) => (idx === index ? { ...stg, isHoldPoint: !stg.isHoldPoint } : stg)),
    );
  };

  // Ghi lại vị trí hiện tại của toàn bộ các phần tử trước khi hoán đổi
  const snapshotPositions = () => {
    prevPositionsRef.current.clear();
    localStages.forEach((stg) => {
      const el = itemRefs.current.get(stg.stageId);
      if (el) {
        prevPositionsRef.current.set(stg.stageId, el.getBoundingClientRect().top);
      }
    });
  };

  const handleMoveStep = (index: number, direction: -1 | 1) => {
    const targetIdx = index + direction;
    if (targetIdx < 0 || targetIdx >= localStages.length) return;

    snapshotPositions();
    const copy = [...localStages];
    const temp = copy[index];
    copy[index] = copy[targetIdx];
    copy[targetIdx] = temp;
    setLocalStages(copy.map((s, idx) => ({ ...s, stageSequence: idx + 1 })));
  };

  // Drag & Drop Handlers với cơ chế phát hiện nửa trên/nửa dưới (Midpoint Swap)
  const handleDragStart = (e: React.DragEvent<HTMLDivElement>, index: number) => {
    setDraggedIndex(index);
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", String(index));
  };

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>, hoverIndex: number) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = "move";

    if (draggedIndex === null || draggedIndex === hoverIndex) return;

    // Tính toán vị trí con trỏ chuột so với nửa trên / nửa dưới của thẻ mục tiêu
    const rect = e.currentTarget.getBoundingClientRect();
    const hoverMiddleY = (rect.bottom - rect.top) / 2;
    const hoverClientY = e.clientY - rect.top;

    // 1. Khi kéo từ trên xuống (draggedIndex < hoverIndex):
    // Chỉ nhảy khi con trỏ đã đi xuống quá nửa dưới của thẻ mục tiêu (hoverClientY > hoverMiddleY)
    if (draggedIndex < hoverIndex && hoverClientY < hoverMiddleY) {
      return;
    }

    // 2. Khi kéo từ dưới lên (draggedIndex > hoverIndex):
    // Chỉ nhảy khi con trỏ đã đi lên quá nửa trên của thẻ mục tiêu (hoverClientY > hoverMiddleY)
    if (draggedIndex > hoverIndex && hoverClientY > hoverMiddleY) {
      return;
    }

    // Đạt điều kiện ngưỡng 50%: Chụp vị trí hiện tại để kích hoạt FLIP animation
    snapshotPositions();

    // Dịch chuyển phần tử ngay lập tức trong lúc đang kéo
    const updated = [...localStages];
    const [movedItem] = updated.splice(draggedIndex, 1);
    updated.splice(hoverIndex, 0, movedItem);

    // Cập nhật lại mảng và cập nhật index hiện tại của phần tử đang kéo
    setLocalStages(updated.map((s, idx) => ({ ...s, stageSequence: idx + 1 })));
    setDraggedIndex(hoverIndex);
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setDraggedIndex(null);
  };

  const handleDragEnd = () => {
    setDraggedIndex(null);
  };

  const handleDeleteStep = (index: number) => {
    if (localStages.length <= 1) return;
    setLocalStages((prev) =>
      prev
        .filter((_, idx) => idx !== index)
        .map((s, idx) => ({ ...s, stageSequence: idx + 1 })),
    );
  };

  const handleAddStep = () => {
    const trimmed = newStepName.trim();
    if (!trimmed) return;
    const nextSeq = localStages.length + 1;
    setLocalStages((prev) => [
      ...prev,
      {
        stageId: Date.now(),
        stageSequence: nextSeq,
        stageName: trimmed,
        isHoldPoint: false,
      },
    ]);
    setNewStepName("");
    setShowAddInput(false);
  };

  const handleSave = () => {
    onSave(localStages.map((s, idx) => ({ ...s, stageSequence: idx + 1 })));
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-xl overflow-hidden">
        {/* Modal Header */}
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
          <div>
            <h2 className="text-base font-bold text-slate-900">
              Cấu hình chuỗi công đoạn sản xuất
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Thiết lập trình tự trạm làm việc và các điểm chặn kiểm soát (Hold Point)
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-xl font-bold leading-none px-1 cursor-pointer"
          >
            ×
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 space-y-4 max-h-[75vh] overflow-y-auto">
          <div className="flex items-center justify-between gap-3">
            <p className="text-sm text-slate-600">
              Nhấn giữ tay nắm <span className="font-mono font-bold text-slate-800">⋮⋮</span> để kéo thả đổi thứ tự, hoặc dùng phím mũi tên.
            </p>
            <button
              type="button"
              onClick={() => setShowAddInput((v) => !v)}
              className="shrink-0 px-3 py-1.5 text-xs font-semibold text-blue-600 hover:bg-blue-50 border border-blue-200 rounded-lg transition cursor-pointer"
            >
              Thêm công đoạn
            </button>
          </div>

          {showAddInput && (
            <div className="flex items-center gap-2 p-3 bg-slate-50 rounded-lg border border-slate-200">
              <input
                type="text"
                value={newStepName}
                onChange={(e) => setNewStepName(e.target.value)}
                placeholder="VD: Bọc nệm da / vải"
                className="flex-1 px-3.5 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-600"
              />
              <button
                type="button"
                onClick={handleAddStep}
                className="px-4 py-2 bg-blue-600 text-white text-sm font-semibold rounded-lg hover:bg-blue-700 transition cursor-pointer"
              >
                Thêm
              </button>
            </div>
          )}

          {/* Workstation Sequence Steps */}
          <div className="space-y-2.5 relative">
            {localStages.map((stg, idx) => {
              const isHold = stg.isHoldPoint;
              const isBeingDragged = draggedIndex === idx;

              return (
                <div
                  key={stg.stageId}
                  ref={(el) => {
                    if (el) {
                      itemRefs.current.set(stg.stageId, el);
                    } else {
                      itemRefs.current.delete(stg.stageId);
                    }
                  }}
                  draggable
                  onDragStart={(e) => handleDragStart(e, idx)}
                  onDragOver={(e) => handleDragOver(e, idx)}
                  onDrop={handleDrop}
                  onDragEnd={handleDragEnd}
                  className={`px-4 py-3 rounded-lg border flex items-center justify-between gap-3 transition-colors duration-150 select-none ${
                    isBeingDragged
                      ? "opacity-60 border-2 border-dashed border-blue-500 bg-blue-50/70 shadow-inner"
                      : isHold
                      ? "border-rose-400 bg-rose-50/30 shadow-2xs hover:bg-rose-50/50"
                      : "border-slate-200 bg-white hover:bg-slate-50 hover:border-slate-300"
                  }`}
                >
                  <div className="flex items-center gap-2.5 min-w-0 flex-1">
                    {/* Grip Handle Icon */}
                    <div
                      className="cursor-grab active:cursor-grabbing text-slate-400 hover:text-blue-600 p-1 rounded transition"
                      title="Giữ chuột kéo để đổi thứ tự công đoạn"
                    >
                      <svg
                        className="w-4 h-4 shrink-0"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth={2}
                        viewBox="0 0 24 24"
                      >
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          d="M8 9h.01M8 15h.01M16 9h.01M16 15h.01M12 9h.01M12 15h.01"
                        />
                      </svg>
                    </div>

                    {/* Up/Down buttons for accessibility */}
                    <div className="flex flex-col gap-0.5 text-xs text-slate-400 shrink-0">
                      <button
                        type="button"
                        onClick={() => handleMoveStep(idx, -1)}
                        disabled={idx === 0}
                        className="hover:text-slate-700 disabled:opacity-20 leading-none cursor-pointer"
                        title="Chuyển lên"
                      >
                        ▲
                      </button>
                      <button
                        type="button"
                        onClick={() => handleMoveStep(idx, 1)}
                        disabled={idx === localStages.length - 1}
                        className="hover:text-slate-700 disabled:opacity-20 leading-none cursor-pointer"
                        title="Chuyển xuống"
                      >
                        ▼
                      </button>
                    </div>

                    <div className="text-sm text-slate-800 truncate ml-1">
                      <strong className="font-bold text-slate-900">
                        Công đoạn {idx + 1}:
                      </strong>{" "}
                      <span>{cleanStageName(stg.stageName)}</span>
                    </div>
                  </div>

                  <div className="flex items-center gap-2 shrink-0">
                    <button
                      type="button"
                      onClick={() => handleToggleHoldPoint(idx)}
                      title="Bấm để chuyển giữa Chuẩn và Hold Point quan trọng"
                      className={`px-2.5 py-1 text-xs font-bold rounded-md transition cursor-pointer ${
                        isHold
                          ? "bg-amber-100 text-amber-800 border border-amber-300"
                          : "bg-slate-100 text-slate-600 hover:bg-slate-200"
                      }`}
                    >
                      {isHold ? "Hold Point (Khóa phụ kiện)" : "Chuẩn"}
                    </button>

                    <button
                      type="button"
                      onClick={() => handleDeleteStep(idx)}
                      disabled={localStages.length <= 1}
                      title={
                        localStages.length <= 1
                          ? "Cần ít nhất 1 công đoạn"
                          : "Xóa công đoạn này"
                      }
                      className="px-2.5 py-1 text-xs font-bold text-rose-600 hover:bg-rose-50 border border-slate-200 rounded-md transition disabled:opacity-30 disabled:cursor-not-allowed cursor-pointer"
                    >
                      ×
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Modal Footer */}
        <div className="px-6 py-4 border-t border-slate-200 bg-[#F8FAFC] flex items-center justify-end gap-2.5">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-300 text-sm font-semibold rounded-lg transition cursor-pointer"
          >
            Đóng
          </button>
          <button
            type="button"
            onClick={handleSave}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white text-sm font-semibold rounded-lg shadow-xs transition cursor-pointer"
          >
            Lưu chuỗi công đoạn
          </button>
        </div>
      </div>
    </div>
  );
}
