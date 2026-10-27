import { ROLE_PERMISSION_MATRIX_ROWS } from "../../../constants/wmmsConstants";

export interface RolePermissionMatrixModalProps {
  isOpen: boolean;
  onClose: () => void;
}

interface PermCell {
  type: string;
  label: string;
}

function renderPermBadge(cell?: PermCell | null) {
  if (!cell) return null;
  if (cell.type === "full") {
    return (
      <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-emerald-100 text-emerald-700">
        {cell.label}
      </span>
    );
  }
  if (cell.type === "monitor") {
    return (
      <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-blue-100 text-blue-700">
        {cell.label}
      </span>
    );
  }
  if (cell.type === "request") {
    return (
      <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-yellow-100 text-amber-800">
        {cell.label}
      </span>
    );
  }
  return (
    <span className="inline-block px-2 py-0.5 text-[10px] font-bold uppercase rounded bg-slate-100 text-slate-600">
      {cell.label}
    </span>
  );
}

/**
 * Modal 3.3: Role Permission Configuration Matrix (MOD-ADM-02 - Section III.3.3)
 */
export default function RolePermissionMatrixModal({
  isOpen,
  onClose,
}: RolePermissionMatrixModalProps) {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-[1px] flex items-center justify-center p-4">
      <div className="w-full max-w-[650px] bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden">
        {/* Header Modal */}
        <div className="px-6 pt-5 pb-3 flex items-center justify-between border-b border-slate-100">
          <h2 className="text-sm font-bold text-slate-900">
            Role Permission Configuration Matrix (MOD-ADM-02)
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1"
          >
            ×
          </button>
        </div>

        {/* Bảng Role Permission Configuration Matrix (Field #2) */}
        <div className="px-6 py-4 overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-slate-200 text-[11px] font-semibold text-slate-400 bg-slate-50/70">
                <th className="py-2.5 px-3">Functional Domain</th>
                <th className="py-2.5 px-3">Action Scope</th>
                <th className="py-2.5 px-3">Workshop Owner</th>
                <th className="py-2.5 px-3">Production Leader</th>
                <th className="py-2.5 px-3">QC Staff</th>
                <th className="py-2.5 px-3">Warehouse</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 text-xs">
              {ROLE_PERMISSION_MATRIX_ROWS.map((row, idx) => (
                <tr key={idx}>
                  <td className="py-3 px-3 font-bold text-slate-800">
                    {row.functionalDomain}
                  </td>
                  <td className="py-3 px-3 text-slate-600">
                    {row.actionScope}
                  </td>
                  <td className="py-3 px-3">
                    {renderPermBadge(row.workshopOwner)}
                  </td>
                  <td className="py-3 px-3">
                    {renderPermBadge(row.productionLeader)}
                  </td>
                  <td className="py-3 px-3">{renderPermBadge(row.qcStaff)}</td>
                  <td className="py-3 px-3">
                    {renderPermBadge(row.warehouse)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Footer: Field #3 Close Button */}
        <div className="px-6 py-3.5 border-t border-slate-100 flex justify-end">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-1.5 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded-md hover:bg-slate-50"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}

