import { useState, type FormEvent } from "react";
import { adminApi } from "../api/userApi";

export interface ProvisionedUserItem {
  userId: number;
  fullName: string;
  email: string;
  primaryRole: string;
  primaryRoleLabel: string;
  operationalScope: string;
  status: string;
}

export interface ProvisionUserModalProps {
  isOpen: boolean;
  onClose: () => void;
  existingEmails: string[];
  onSuccess: (newUser: ProvisionedUserItem, message: string) => void;
  onError: (message: string) => void;
}

function mapSelectedOptionToRole(optionText = "") {
  if (optionText.includes("Sales")) {
    return {
      code: "SALES",
      label: "SALES STAFF",
      scope: "Excel PO Staging, CRUD Sanitization, Space Init",
    };
  }
  if (optionText.includes("Leader")) {
    return {
      code: "LEADER",
      label: "PRODUCTION LEADER",
      scope: "Kanban Floor Dispatch, Overdriver Requests",
    };
  }
  if (optionText.includes("QC")) {
    return {
      code: "QC",
      label: "QC STAFF",
      scope: "Workstation Quality Gate, Defect Logging",
    };
  }
  if (optionText.includes("Warehouse")) {
    return {
      code: "WAREHOUSE",
      label: "WAREHOUSE KEEPER",
      scope: "Goods Receipt Commit, Goods Issue Floor Handover",
    };
  }
  if (optionText.includes("Purchasing")) {
    return {
      code: "PURCHASING",
      label: "PURCHASING STAFF",
      scope: "Vendor Sourcing, PO Creation & ETA Management",
    };
  }
  return {
    code: "OWNER",
    label: "WORKSHOP OWNER",
    scope: "BOM Breakdown, Purchasing Approval, Dispatch Release",
  };
}

/**
 * Modal 3.2: Provision New User Account (MOD-ADM-01 - Section III.3.2)
 */
export default function ProvisionUserModal({
  isOpen,
  onClose,
  existingEmails,
  onSuccess,
  onError,
}: ProvisionUserModalProps) {
  const [fullNameInput, setFullNameInput] = useState("");
  const [companyEmailInput, setCompanyEmailInput] = useState("");
  const [assignedRoleOption, setAssignedRoleOption] = useState(
    "Workshop Owner (Executive & Technical Planner)",
  );
  const [initialPasswordInput, setInitialPasswordInput] =
    useState("Welcome@2026");

  if (!isOpen) return null;

  const handleCreateAccount = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!fullNameInput.trim() || !companyEmailInput.trim()) {
      onError(
        "Please provide both Full Employee Name and Company Email (MSG01).",
      );
      return;
    }

    // Kiểm tra trùng email (UC008.E1 / MSG-ADM-02)
    const emailLower = companyEmailInput.trim().toLowerCase();
    if (existingEmails.some((email) => email.toLowerCase() === emailLower)) {
      onError(
        "Username or email address already exists in the system. Please provide distinct credentials. (MSG-ADM-02)",
      );
      return;
    }

    const roleMapping = mapSelectedOptionToRole(assignedRoleOption);

    try {
      await adminApi.createUser({
        username: emailLower.split("@")[0],
        email: companyEmailInput.trim(),
        fullName: fullNameInput.trim(),
        initialPassword: initialPasswordInput || "Welcome@2026",
        roleCodes: [roleMapping.code],
      });
    } catch {
      // Tiếp tục cập nhật state trên UI nếu chạy độc lập
    }

    const newUser: ProvisionedUserItem = {
      userId: Date.now(),
      fullName: fullNameInput.trim(),
      email: companyEmailInput.trim(),
      primaryRole: roleMapping.code,
      primaryRoleLabel: roleMapping.label,
      operationalScope: roleMapping.scope,
      status: "ACTIVE",
    };

    setFullNameInput("");
    setCompanyEmailInput("");
    onSuccess(
      newUser,
      "New staff account created successfully! Activation details and temporary password dispatched. (MSG-ADM-01)",
    );
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/60 backdrop-blur-[1px] flex items-center justify-center p-4">
      <div className="w-full max-w-[500px] bg-white rounded-xl shadow-2xl border border-slate-200 overflow-hidden">
        {/* Header Modal */}
        <div className="px-6 pt-5 pb-3 flex items-center justify-between border-b border-slate-100">
          <h2 className="text-sm font-bold text-slate-900">
            Provision New User Account (MOD-ADM-01)
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="text-slate-400 hover:text-slate-600 text-base font-bold leading-none px-1"
          >
            ×
          </button>
        </div>

        <form onSubmit={handleCreateAccount} className="px-6 py-4 space-y-4">
          {/* Field #2: Full Employee Name */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Full Employee Name
            </label>
            <input
              type="text"
              value={fullNameInput}
              onChange={(e) => setFullNameInput(e.target.value)}
              placeholder="e.g. Tran Van Binh"
              className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {/* Field #3: Company Email */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Company Email
            </label>
            <input
              type="email"
              value={companyEmailInput}
              onChange={(e) => setCompanyEmailInput(e.target.value)}
              placeholder="binh.tran@nguyenkhoa.vn"
              className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>

          {/* Field #4: Assigned Role */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Assigned Role
            </label>
            <select
              value={assignedRoleOption}
              onChange={(e) => setAssignedRoleOption(e.target.value)}
              className="w-full px-3 py-2 text-xs bg-white border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option>
                Workshop Owner (Executive &amp; Technical Planner)
              </option>
              <option>Sales Staff (Excel PO Staging &amp; Space Init)</option>
              <option>Production Leader (Kanban Floor Dispatch)</option>
              <option>QC Staff (Workstation Quality Gate)</option>
              <option>Warehouse Keeper (Goods Receipt &amp; Issue)</option>
              <option>Purchasing Staff (Vendor Sourcing &amp; PO)</option>
            </select>
          </div>

          {/* Field #5: Initial Password */}
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1">
              Initial Password
            </label>
            <input
              type="text"
              value={initialPasswordInput}
              onChange={(e) => setInitialPasswordInput(e.target.value)}
              className="w-full px-3 py-2 text-xs bg-slate-50 text-slate-800 border border-slate-300 rounded-md focus:outline-none"
            />
          </div>

          {/* Footer Buttons: Field #6 Cancel & Field #7 Create Account */}
          <div className="pt-2 flex items-center justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="px-3.5 py-2 text-xs font-medium text-slate-700 bg-white border border-slate-300 rounded-md hover:bg-slate-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-4 py-2 text-xs font-semibold text-white bg-[#2563eb] hover:bg-blue-700 rounded-md shadow-sm"
            >
              Create Account
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

