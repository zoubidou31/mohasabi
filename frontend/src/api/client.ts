import axios, { AxiosError } from 'axios';

export const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.response.use(
  (res) => res,
  (error) => {
    if (axios.isAxiosError(error)) {
      const err = error as AxiosError;
      if (err.code === 'ERR_NETWORK' || err.code === 'ERR_CONNECTION_REFUSED') {
        console.error('[API] Serveur indisponible:', err.message);
      }
    }
    return Promise.reject(error);
  },
);

export interface ApiError {
  status?: number;
  message?: string;
}

export function extractError(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const axiosError = error as AxiosError<{ message?: string; title?: string; errors?: Record<string, string[]> }>;

    if (axiosError.code === 'ERR_NETWORK' || axiosError.code === 'ERR_CONNECTION_REFUSED') {
      return 'Serveur indisponible. Vérifiez que Mohasabi est démarré.';
    }

    const status = axiosError.response?.status;

    if (status === 400) {
      const data = axiosError.response?.data as Record<string, unknown> | undefined;
      if (data?.errors) {
        const flat = Object.values(data.errors as Record<string, string[]>).flat();
        if (flat.length > 0) return flat.join('\n');
      }
      if (typeof data?.message === 'string') return data.message;
      return 'Données invalides. Vérifiez les champs saisis.';
    }
    if (status === 401) return 'Non autorisé.';
    if (status === 403) return 'Accès refusé.';
    if (status === 404) {
      const data = axiosError.response?.data as { message?: string } | undefined;
      return data?.message ?? 'Ressource introuvable.';
    }
    if (status === 409) {
      const data = axiosError.response?.data as { message?: string } | undefined;
      return data?.message ?? 'Conflit : l\u2019opération est impossible dans l\u2019état courant de la ressource.';
    }
    if (status === 500) return 'Erreur interne du serveur.';

    if (axiosError.response?.data) {
      const data = axiosError.response.data as { message?: string; title?: string };
      if (data.message) return data.message;
      if (data.title) return data.title;
    }
    if (axiosError.message) {
      return axiosError.message;
    }
  }
  return error instanceof Error ? error.message : 'Erreur inconnue';
}
