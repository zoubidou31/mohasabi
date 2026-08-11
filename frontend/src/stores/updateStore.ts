import { create } from 'zustand';
import { api, extractError } from '../api/client';

export interface UpdateInfo {
  updateAvailable: boolean;
  latestVersion?: string;
  releaseNotes?: string;
  currentVersion?: string;
  sizeBytes?: number | null;
}

export interface UpdateInstallResult {
  message?: string;
  restarting?: boolean;
}

export interface UpdateInstallStatus {
  phase: string;
  downloadedBytes: number;
  totalBytes?: number | null;
  percent?: number | null;
  message?: string;
  error?: string;
}

interface UpdateState extends UpdateInfo {
  checked: boolean;
  dialogOpen: boolean;
  dismissed: boolean;
  installing: boolean;
  installError: string;
  installStatus: UpdateInstallStatus | null;
  setUpdate: (info: UpdateInfo) => void;
  openDialog: () => void;
  dismissDialog: () => void;
  setInstallStatus: (status: UpdateInstallStatus | null) => void;
  installNow: (launchAfterUpdate?: boolean) => Promise<UpdateInstallResult | undefined>;
  reset: () => void;
}

export const useUpdateStore = create<UpdateState>((set, get) => ({
  updateAvailable: false,
  latestVersion: undefined,
  releaseNotes: undefined,
  currentVersion: undefined,
  sizeBytes: undefined,
  checked: false,
  dialogOpen: false,
  dismissed: false,
  installing: false,
  installError: '',
  installStatus: null,
  setUpdate: (info) => set({ ...info, checked: true }),
  openDialog: () => set({ dialogOpen: true }),
  dismissDialog: () => set({ dialogOpen: false, dismissed: true }),
  setInstallStatus: (status) => set({ installStatus: status }),
  installNow: async (launchAfterUpdate = true) => {
    if (get().installing) return undefined;
    set({ installing: true, installError: '', installStatus: null });
    try {
      const { data } = await api.post<UpdateInstallResult>('/update/install', { launchAfterUpdate });
      set({ dialogOpen: false });
      return data;
    } catch (err) {
      set({ installError: extractError(err) });
      return undefined;
    } finally {
      set({ installing: false });
    }
  },
  reset: () =>
    set({
      updateAvailable: false,
      latestVersion: undefined,
      releaseNotes: undefined,
      currentVersion: undefined,
      sizeBytes: undefined,
      checked: false,
      dialogOpen: false,
      dismissed: false,
      installing: false,
      installError: '',
      installStatus: null,
    }),
}));
