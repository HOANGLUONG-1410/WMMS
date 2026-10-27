import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { useSession } from "../store/SessionContext";

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
    onSuccess: (user) => {
      // Điều hướng tới workspace tương ứng với vai trò (POST-2 trong UC-01)
      if (user.activeRole === "ADMIN") {
        navigate("/admin/users");
      }
    },
  });
}
