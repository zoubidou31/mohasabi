import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import '@fontsource/inter/400.css';
import '@fontsource/inter/500.css';
import '@fontsource/inter/600.css';
import '@fontsource/inter/700.css';
import '@fontsource/inter/800.css';
import App from './App';
import './i18n';
import './styles/global.css';

// Applique immédiatement le thème stocké (avant le premier rendu) pour éviter
// un flash clair/sombre : la classe "dark" alimente les variables CSS globales.
const storedTheme = localStorage.getItem('mohasabi_theme');
if (
  storedTheme === 'dark' ||
  (storedTheme === 'system' && window.matchMedia?.('(prefers-color-scheme: dark)').matches)
) {
  document.documentElement.classList.add('dark');
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
);
