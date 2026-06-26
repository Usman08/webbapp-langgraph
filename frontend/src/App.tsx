import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import { Workspace } from "./pages/Workspace";

const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000 } },
});

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Workspace />} />
          <Route path="*" element={<Workspace />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
