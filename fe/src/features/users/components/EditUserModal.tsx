import { useState, useEffect, type FormEvent } from "react";
import type { ProvisionedUserItem } from "./ProvisionUserModal";

export interface EditUserModalProps {
  user: ProvisionedUserItem | null;
  onClose: () => void;
  onSave: (updatedUser: ProvisionedUserItem) => void;
  onSuspendOrBan: (user: ProvisionedUserItem) => void;
}

/**
 * Modal Edit User (UC-09 & UC-10)
 */
export default function EditUserModal({
  user,
  onClose,
  onSave,
  onSuspendOrBan,
}: EditUserModalProps) {
  const [formState, setFormState] = useState<ProvisionedUserItem | null>(user);

  useEffect(() => {
    setFormState(user ? { ...user } : null);
  }, [user]);

  if (!formState) return null;

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    onSave(formState);
  };

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-[1px] flex items-center justify-center p-4">
      <div className="w-full max-w-[460px] bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden">
        <div className="px-6 pt-5 pb-3 flex items-center justify-between border-b border-slate-100">
          <h2 className="text-sm font-bold text-slate-900">
            Edit Employee Account &amp; Role (UC-09)
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1"
          >
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit} className="px-6 py-4 space-y-3.5">
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Employee Name
            </label>
            <input
              type="text"
              value={formState.fullName}
              onChange={(e) =>
                setFormState((prev) =>
                  prev ? { ...prev, fullName: e.target.value } : null,
                )
              }
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Email Address
            </label>
            <input
              type="email"
              value={formState.email}
              onChange={(e) =>
                setFormState((prev) =>
                  prev ? { ...prev, email: e.target.value } : null,
                )
              }
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Operational Scope
            </label>
            <input
              type="text"
              value={formState.operationalScope}
              onChange={(e) =>
                setFormState((prev) =>
                  prev ? { ...prev, operationalScope: e.target.value } : null,
                )
              }
              className="w-full px-3 py-2 text-xs border border-slate-300 rounded-md"
            />
          </div>

          <div className="pt-2 flex items-center justify-between">
            <button
              type="button"
              onClick={() => {
                onSuspendOrBan(formState);
                onClose();
              }}
              className="px-3 py-1.5 text-xs font-semibold text-rose-700 bg-rose-50 border border-rose-200 rounded-md hover:bg-rose-100"
            >
              Suspend / Ban Account
            </button>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={onClose}
                className="px-3.5 py-1.5 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded-md"
              >
                Cancel
              </button>
              <button
                type="submit"
                className="px-4 py-1.5 text-xs font-semibold text-white bg-[#2563eb] hover:bg-blue-700 rounded-md"
              >
                Save Changes
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}

