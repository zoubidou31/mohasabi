import { useEffect, useMemo } from 'react';
import { CssBaseline, ThemeProvider } from '@mui/material';
import { Navigate, createBrowserRouter, RouterProvider } from 'react-router-dom';
import { createAppTheme } from './theme';
import AppLayout from './layout/AppLayout';
import InvoicesPage from './pages/InvoicesPage';
import InvoiceFormPage from './pages/InvoiceFormPage';
import InvoiceDetailPage from './pages/InvoiceDetailPage';
import ClientsPage from './pages/ClientsPage';
import ProductsPage from './pages/ProductsPage';
import ReportsPage from './pages/ReportsPage';
import CompanyPage from './pages/CompanyPage';
import OptionsPage from './pages/OptionsPage';
import { useSettingsStore, resolveTheme } from './stores/settingsStore';
import { ErrorBoundary } from './components/ErrorBoundary';

export default function App() {
  const themePreference = useSettingsStore((s) => s.settings?.theme) ?? localStorage.getItem('mohasabi_theme') ?? 'light';
  const load = useSettingsStore((s) => s.load);

  useEffect(() => {
    void load();
  }, [load]);

  const theme = useMemo(() => createAppTheme(resolveTheme(themePreference)), [themePreference]);

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <ErrorBoundary>
        <RouterProvider router={router} />
      </ErrorBoundary>
    </ThemeProvider>
  );
}

const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <Navigate to="/invoices" replace /> },
      { path: 'invoices', element: <InvoicesPage /> },
      { path: 'invoices/new', element: <InvoiceFormPage /> },
      { path: 'invoices/:id', element: <InvoiceDetailPage /> },
      { path: 'invoices/:id/edit', element: <InvoiceFormPage /> },
      { path: 'clients', element: <ClientsPage /> },
      { path: 'products', element: <ProductsPage /> },
      { path: 'reports', element: <ReportsPage /> },
      { path: 'settings', element: <CompanyPage /> },
      { path: 'options', element: <OptionsPage /> },
    ],
  },
  { path: '*', element: <Navigate to="/invoices" replace /> },
]);
