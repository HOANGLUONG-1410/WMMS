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
      <span className="inline-block px-2.5 py-0.5 text-xs font-bold uppercase rounded bg-emerald-50 text-emerald-700 border border-emerald-200 whitespace-nowrap">
        {cell.label}
      </span>
    );
  }
  if (cell.type === "monitor") {
    return (
      <span className="inline-block px-2.5 py-0.5 text-xs font-bold uppercase rounded bg-blue-50 text-blue-700 border border-blue-200 whitespace-nowrap">
        {cell.label}
      </span>
    );
  }
  if (cell.type === "request") {
    return (
      <span className="inline-block px-2.5 py-0.5 text-xs font-bold uppercase rounded bg-amber-50 text-amber-800 border border-amber-200 whitespace-nowrap">
        {cell.label}
      </span>
    );
  }
  return (
    <span className="inline-block px-2.5 py-0.5 text-xs font-bold uppercase rounded bg-slate-100 text-slate-600 border border-slate-200 whitespace-nowrap">
      {cell.label}
    </span>
  );
}

export default function RolePermissionMatrixModal({
  isOpen,
  onClose,
}: RolePermissionMatrixModalProps) {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/50 backdrop-blur-[1px] flex items-center justify-center p-4">
      <div className="w-full max-w-[780px] bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden">
        <div className="px-6 py-4 flex items-center justify-between border-b border-slate-200">
          <div>
            <h2 className="text-base font-bold text-slate-900">
              Ma trận phân quyền hệ thống
            </h2>
            <p className="text-xs text-slate-500 mt-0.5">
              Phân định phạm vi thao tác giữa các vai trò trong xưởng sản xuất
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

        <div className="px-6 py-5 overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="border-b border-slate-200 text-xs font-bold uppercase tracking-wider text-slate-500 bg-[#F8FAFC]">
                <th className="py-3 px-4">Phân hệ chức năng</th>
                <th className="py-3 px-4">Phạm vi thao tác</th>
                <th className="py-3 px-4">Chủ xưởng</th>
                <th className="py-3 px-4">Quản đốc</th>
                <th className="py-3 px-4">Nhân viên QC</th>
                <th className="py-3 px-4">Thủ kho</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 text-sm">
              {ROLE_PERMISSION_MATRIX_ROWS.map((row, idx) => (
                <tr key={idx} className="hover:bg-slate-50/80">
                  <td className="py-3.5 px-4 font-semibold text-slate-900">
                    {row.functionalDomain}
                  </td>
                  <td className="py-3.5 px-4 text-slate-600">
                    {row.actionScope}
                  </td>
                  <td className="py-3.5 px-4">
                    {renderPermBadge(row.workshopOwner)}
                  </td>
                  <td className="py-3.5 px-4">
                    {renderPermBadge(row.productionLeader)}
                  </td>
                  <td className="py-3.5 px-4">{renderPermBadge(row.qcStaff)}</td>
                  <td className="py-3.5 px-4">
                    {renderPermBadge(row.warehouse)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="px-6 py-4 border-t border-slate-200 bg-[#F8FAFC] flex justify-end">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 text-sm font-semibold text-slate-700 bg-white border border-slate-300 rounded-lg hover:bg-slate-50 cursor-pointer"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
}

