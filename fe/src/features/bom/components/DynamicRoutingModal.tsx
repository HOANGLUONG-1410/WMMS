import { useEffect, useState, type CSSProperties } from "react";
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  DragOverlay,
  type DragStartEvent,
  type DragEndEvent,
} from "@dnd-kit/core";
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  defaultAnimateLayoutChanges,
  verticalListSortingStrategy,
} from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import type { RoutingStageDto } from "../types";

interface DynamicRoutingModalProps {
  isOpen: boolean;
  stages: RoutingStageDto[];
  onClose: () => void;
  onSave: (updatedStages: RoutingStageDto[]) => void;
}

// Làm sạch tiền tố số cũ (ví dụ "1. Pha phôi" hoặc "Step 8: Tốt nghiệp") để tránh trùng "Công đoạn 1: 3. Pha phôi" khi đổi vị trí
const cleanStageName = (name: string) => {
  return (name || "").replace(/^\s*(Step\s*\d+\s*:\s*|\d+\s*[\.\:\-]\s*)/i, "").trim();
};

interface StageCardContentProps {
  stage: RoutingStageDto;
  index: number;
  totalCount: number;
  isOverlay?: boolean;
  onToggleHoldPoint?: (index: number) => void;
  onDeleteStep?: (index: number) => void;
}

function StageCardContent({
  stage,
  index,
  totalCount,
  isOverlay = false,
  onToggleHoldPoint,
  onDeleteStep,
}: StageCardContentProps) {
  const isHold = stage.isHoldPoint;

  return (
    <div
      className={`px-4 py-3 rounded-lg border flex items-center justify-between gap-3 select-none touch-none ${
        isOverlay
          ? "bg-white border-blue-500 ring-2 ring-blue-500/20 shadow-xl cursor-grabbing"
          : isHold
          ? "border-rose-400 bg-rose-50/30 shadow-2xs hover:bg-rose-50/50 cursor-grab"
          : "border-slate-200 bg-white hover:bg-slate-50 hover:border-slate-300 cursor-grab"
      }`}
    >
      <div className="flex items-center gap-2.5 min-w-0 flex-1">
        {/* Grip Handle Icon */}
        <div
          className={`p-1 rounded transition ${
            isOverlay ? "text-blue-600" : "text-slate-400"
          }`}
          title="Nhấn giữ và kéo để đổi thứ tự công đoạn"
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

        <div className="text-sm text-slate-800 truncate">
          <strong className="font-bold text-slate-900">
            Công đoạn {index + 1}:
          </strong>{" "}
          <span>{cleanStageName(stage.stageName)}</span>
        </div>
      </div>

      <div
        className="flex items-center gap-2 shrink-0"
        onPointerDown={(e) => e.stopPropagation()}
      >
        <button
          type="button"
          onClick={() => onToggleHoldPoint?.(index)}
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
          onClick={() => onDeleteStep?.(index)}
          disabled={totalCount <= 1}
          title={totalCount <= 1 ? "Cần ít nhất 1 công đoạn" : "Xóa công đoạn này"}
          className="px-2.5 py-1 text-xs font-bold text-rose-600 hover:bg-rose-50 border border-slate-200 rounded-md transition disabled:opacity-30 disabled:cursor-not-allowed cursor-pointer"
        >
          ×
        </button>
      </div>
    </div>
  );
}

interface SortableStageItemProps {
  stage: RoutingStageDto;
  index: number;
  totalCount: number;
  onToggleHoldPoint: (index: number) => void;
  onDeleteStep: (index: number) => void;
}

function SortableStageItem({
  stage,
  index,
  totalCount,
  onToggleHoldPoint,
  onDeleteStep,
}: SortableStageItemProps) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({
    id: stage.stageId,
    transition: {
      duration: 220,
      easing: "cubic-bezier(0.25, 1, 0.5, 1)",
    },
    animateLayoutChanges: (args) =>
      defaultAnimateLayoutChanges({ ...args, wasDragging: true }),
  });

  const style: CSSProperties = {
    // Khóa trục x để chuyển động mượt mà thẳng hàng tuyệt đối theo phương dọc
    transform: transform ? CSS.Translate.toString({ ...transform, x: 0 }) : undefined,
    transition,
    willChange: "transform",
  };

  return (
    <div
      ref={setNodeRef}
      style={style}
      {...attributes}
      {...listeners}
      className={isDragging ? "opacity-0 pointer-events-none" : undefined}
    >
      <StageCardContent
        stage={stage}
        index={index}
        totalCount={totalCount}
        onToggleHoldPoint={onToggleHoldPoint}
        onDeleteStep={onDeleteStep}
      />
    </div>
  );
}

export default function DynamicRoutingModal({
  isOpen,
  stages,
  onClose,
  onSave,
}: DynamicRoutingModalProps) {
  const [localStages, setLocalStages] = useState<RoutingStageDto[]>(stages);
  const [activeId, setActiveId] = useState<number | string | null>(null);
  const [newStepName, setNewStepName] = useState("");
  const [showAddInput, setShowAddInput] = useState(false);

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 4,
      },
    }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  );

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
      setActiveId(null);
    }
  }, [isOpen, stages]);

  if (!isOpen) return null;

  const handleToggleHoldPoint = (index: number) => {
    setLocalStages((prev) =>
      prev.map((stg, idx) =>
        idx === index ? { ...stg, isHoldPoint: !stg.isHoldPoint } : stg,
      ),
    );
  };

  const handleDragStart = (event: DragStartEvent) => {
    setActiveId(event.active.id);
  };

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    setActiveId(null);

    if (!over || active.id === over.id) return;

    setLocalStages((prev) => {
      const oldIndex = prev.findIndex((s) => s.stageId === active.id);
      const newIndex = prev.findIndex((s) => s.stageId === over.id);
      if (oldIndex === -1 || newIndex === -1) return prev;
      return arrayMove(prev, oldIndex, newIndex).map((s, idx) => ({
        ...s,
        stageSequence: idx + 1,
      }));
    });
  };

  const handleDragCancel = () => {
    setActiveId(null);
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

  const activeStage = localStages.find((s) => s.stageId === activeId);
  const activeIndex = localStages.findIndex((s) => s.stageId === activeId);

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
        <div className="p-6 space-y-4 max-h-[75vh] overflow-y-auto overflow-x-hidden">
          <div className="flex items-center justify-between gap-3">
            <p className="text-sm text-slate-600">
              Kéo thả các thẻ công đoạn để sắp xếp thứ tự thực hiện.
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

          {/* Workstation Sequence Steps with @dnd-kit */}
          <DndContext
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragStart={handleDragStart}
            onDragEnd={handleDragEnd}
            onDragCancel={handleDragCancel}
          >
            <SortableContext
              items={localStages.map((s) => s.stageId)}
              strategy={verticalListSortingStrategy}
            >
              <div className="space-y-2.5">
                {localStages.map((stg, idx) => (
                  <SortableStageItem
                    key={stg.stageId}
                    stage={stg}
                    index={idx}
                    totalCount={localStages.length}
                    onToggleHoldPoint={handleToggleHoldPoint}
                    onDeleteStep={handleDeleteStep}
                  />
                ))}
              </div>
            </SortableContext>

            {/* Floating Layer for 60fps drag preview */}
            <DragOverlay
              dropAnimation={{
                duration: 200,
                easing: "cubic-bezier(0.18, 0.67, 0.6, 1.22)",
              }}
            >
              {activeStage && (
                <StageCardContent
                  stage={activeStage}
                  index={activeIndex}
                  totalCount={localStages.length}
                  isOverlay
                />
              )}
            </DragOverlay>
          </DndContext>
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
