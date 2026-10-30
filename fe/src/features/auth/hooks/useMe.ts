import { useSession } from "../store/SessionContext";

export function useMe() {
  const { currentUser, isInitializing, switchActiveRole } = useSession();

  return {
    currentUser,
    isInitializing,
    isAuthenticated: Boolean(currentUser),
    activeRole: currentUser?.activeRole,
    roles: currentUser?.roles ?? [],
    switchActiveRole,
  };
}

