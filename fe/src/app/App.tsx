import { RouterProvider } from "react-router-dom";
import { Toaster } from "sonner";
import { QueryProvider } from "./providers/QueryProvider";
import { WmmsProvider } from "../features/auth/hooks/WmmsContext";
import { SessionProvider } from "../features/auth/store/SessionContext";
import { router } from "./router";

/**
 * Root App Composition:
 * Gom toàn bộ Providers toàn cục theo thứ tự phân tầng chuẩn:
 * 1. QueryProvider (TanStack Query Cache + DevTools)
 * 2. WmmsProvider (Mock Store không gian đơn hàng, logs)
 * 3. SessionProvider (Phiên làm việc người dùng, Silent Refresh, Role Switcher)
 * 4. RouterProvider (Bộ định tuyến createBrowserRouter)
 * 5. Toaster (Thông báo Toast góc phải)
 */
export default function App() {
  return (
    <QueryProvider>
      <WmmsProvider>
        <SessionProvider>
          <RouterProvider router={router} />
          <Toaster position="top-right" richColors closeButton />
        </SessionProvider>
      </WmmsProvider>
    </QueryProvider>
  );
}

