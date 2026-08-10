import { useEffect, useMemo } from 'react';
import { CssBaseline, ThemeProvider } from '@mui/material';
import { Navigate, Route, Routes } from 'react-router-dom';
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
        <Routes>
          <Route path="/" element={<AppLayout />}>
            <Route index element={<Navigate to="/invoices" replace />} />
            <Route path="invoices" element={<InvoicesPage />} />
            <Route path="invoices/new" element={<InvoiceFormPage />} />
            <Route path="invoices/:id" element={<InvoiceDetailPage />} />
            <Route path="invoices/:id/edit" element={<InvoiceFormPage />} />
            <Route path="clients" element={<ClientsPage />} />
            <Route path="products" element={<ProductsPage />} />
            <Route path="reports" element={<ReportsPage />} />
            <Route path="settings" element={<CompanyPage />} />
            <Route path="options" element={<OptionsPage />} />
          </Route>
          <Route path="*" element={<Navigate to="/invoices" replace />} />
        </Routes>
      </ErrorBoundary>
    </ThemeProvider>
  );
}
