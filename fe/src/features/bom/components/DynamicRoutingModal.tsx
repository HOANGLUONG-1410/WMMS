import { useEffect, useState } from "react";
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

  useEffect(() => {
    if (isOpen) {
      setLocalStages(stages.map((s, idx) => ({ ...s, stageSequence: idx + 1 })));
      setShowAddInput(false);
      setNewStepName("");
    }
  }, [isOpen, stages]);

  if (!isOpen) return null;

  const handleToggleHoldPoint = (index: number) => {
    setLocalStages((prev) =>
      prev.map((stg, idx) => (idx === index ? { ...stg, isHoldPoint: !stg.isHoldPoint } : stg)),
    );
  };

  const handleMoveStep = (index: number, direction: -1 | 1) => {
    const targetIdx = index + direction;
    if (targetIdx < 0 || targetIdx >= localStages.length) return;
    const copy = [...localStages];
    const temp = copy[index];
    copy[index] = copy[targetIdx];
    copy[targetIdx] = temp;
    setLocalStages(copy.map((s, idx) => ({ ...s, stageSequence: idx + 1 })));
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
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-[1px]">
      <div className="bg-white rounded-xl shadow-2xl border border-slate-200 w-full max-w-xl overflow-hidden">
        {/* Modal Header */}
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
          <h2 className="text-sm font-extrabold text-slate-900">
            Customize Dynamic Routing Sequence
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1.5 py-0.5 rounded"
          >
            ✕
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 space-y-4 max-h-[75vh] overflow-y-auto">
          <div className="flex items-center justify-between gap-2">
            <p className="text-xs text-slate-500">
              Re-order workstation sequence, inject specialized steps (e.g., Cushion Upholstery), or
              mark critical Hold Points.
            </p>
            <button
              type="button"
              onClick={() => setShowAddInput((v) => !v)}
              className="shrink-0 px-2.5 py-1 text-[11px] font-semibold text-blue-600 hover:bg-blue-50 border border-blue-200 rounded transition"
            >
              + Inject Step
            </button>
          </div>

          {showAddInput && (
            <div className="flex items-center gap-2 p-2.5 bg-slate-50 rounded-lg border border-slate-200">
              <input
                type="text"
                value={newStepName}
                onChange={(e) => setNewStepName(e.target.value)}
                placeholder="e.g., Cushion Upholstery (Bọc nệm da/vải)"
                className="flex-1 px-3 py-1.5 text-xs bg-white border border-slate-300 rounded focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              <button
                type="button"
                onClick={handleAddStep}
                className="px-3 py-1.5 bg-[#2563eb] text-white text-xs font-semibold rounded hover:bg-blue-700 transition"
              >
                Add
              </button>
            </div>
          )}

          {/* Workstation Sequence Steps */}
          <div className="space-y-2.5">
            {localStages.map((stg, idx) => {
              const isHold = stg.isHoldPoint;
              return (
                <div
                  key={stg.stageId}
                  className={`px-4 py-3 rounded-lg border flex items-center justify-between gap-3 transition ${
                    isHold
                      ? "border-rose-400 bg-rose-50/20 shadow-2xs"
                      : "border-slate-200 bg-white hover:bg-slate-50/70"
                  }`}
                >
                  <div className="flex items-center gap-2.5 min-w-0">
                    <div className="flex flex-col gap-0.5 text-[10px] text-slate-400">
                      <button
                        type="button"
                        onClick={() => handleMoveStep(idx, -1)}
                        disabled={idx === 0}
                        className="hover:text-slate-700 disabled:opacity-25 leading-none"
                        title="Move Up"
                      >
                        ▲
                      </button>
                      <button
                        type="button"
                        onClick={() => handleMoveStep(idx, 1)}
                        disabled={idx === localStages.length - 1}
                        className="hover:text-slate-700 disabled:opacity-25 leading-none"
                        title="Move Down"
                      >
                        ▼
                      </button>
                    </div>
                    <div className="text-xs text-slate-800 truncate">
                      <strong className="font-bold text-slate-900">
                        Step {idx + 1}:
                      </strong>{" "}
                      <span>{stg.stageName}</span>
                    </div>
                  </div>

                  <button
                    type="button"
                    onClick={() => handleToggleHoldPoint(idx)}
                    title="Click to toggle Standard vs Critical Hold Point"
                    className={`shrink-0 px-2.5 py-0.5 text-[10px] font-bold rounded transition cursor-pointer ${
                      isHold
                        ? "bg-yellow-100 text-amber-800 border border-yellow-300"
                        : "bg-slate-100 text-slate-600 hover:bg-slate-200"
                    }`}
                  >
                    {isHold ? "Critical Hold Point (Hardware Lock)" : "STANDARD"}
                  </button>
                </div>
              );
            })}
          </div>
        </div>

        {/* Modal Footer */}
        <div className="px-6 py-3.5 border-t border-slate-200 bg-slate-50/50 flex items-center justify-end gap-2.5">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 bg-white hover:bg-slate-100 text-slate-700 border border-slate-300 text-xs font-semibold rounded-md transition"
          >
            Close
          </button>
          <button
            type="button"
            onClick={handleSave}
            className="px-4 py-2 bg-[#2563eb] hover:bg-blue-700 text-white text-xs font-semibold rounded-md shadow-sm transition"
          >
            Save Routing Sequence
          </button>
        </div>
      </div>
    </div>
  );
}

