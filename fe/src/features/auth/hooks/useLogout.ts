import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { useSession } from "../store/SessionContext";

export function useLogout() {
  const { logout } = useSession();
  const navigate = useNavigate();

  return useMutation({
    mutationFn: () => logout(),
    onSuccess: () => {
      navigate("/login", { replace: true });
    },
  });
}

