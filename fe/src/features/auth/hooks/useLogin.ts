import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { useSession } from "../store/SessionContext";

const ROLE_DEFAULT_ROUTES: Partial<Record<string, string>> = {
  ADMIN: "/admin/users",
  PURCHASING: "/purchasing/orders",
  PLANNER: "/bom",
  SALES: "/orders",
  OWNER: "/orders",
  WAREHOUSE: "/purchasing/orders",
};

export function useLogin() {
  const { login } = useSession();
  const navigate = useNavigate();

  return useMutation({
    mutationFn: ({
      usernameOrEmail,
      password,
    }: {
      usernameOrEmail: string;
      password: string;
    }) => login(usernameOrEmail, password),
    meta: {
      // Bỏ qua toast pop-up toàn cục khi login thất bại (lỗi hiển thị trực tiếp trên form)
      skipGlobalErrorToast: true,
    },
    onSuccess: ({ user, message }) => {
      // Hiển thị thông báo thành công từ Backend ở pop-up góc trên bên phải
      toast.success(message || "Signed in successfully!", {
        id: "login-success",
      });

      // Điều hướng tới workspace tương ứng với vai trò của người dùng
      const targetRoute =
        (user.activeRole && ROLE_DEFAULT_ROUTES[user.activeRole]) || "/orders";
      navigate(targetRoute, { replace: true });
    },
  });
}
