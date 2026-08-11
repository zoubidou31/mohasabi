import { useEffect, useRef } from 'react';

export const SHORTCUT_EVENTS = {
  SAVE: 'mohasabi:save',
  FOCUS_SEARCH: 'mohasabi:focus-search',
} as const;

export function dispatchShortcut(event: string) {
  window.dispatchEvent(new Event(event));
}

export function useShortcutEvent(event: string, handler: () => void) {
  const ref = useRef(handler);
  ref.current = handler;

  useEffect(() => {
    const listener = () => ref.current();
    window.addEventListener(event, listener);
    return () => window.removeEventListener(event, listener);
  }, [event]);
}

interface GlobalShortcutHandlers {
  onNewInvoice: () => void;
  onSave: () => void;
  onFocusSearch: () => void;
}

function isEditableTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  return (
    tag === 'INPUT' ||
    tag === 'TEXTAREA' ||
    tag === 'SELECT' ||
    target.isContentEditable
  );
}

export function useGlobalShortcuts(handlers: GlobalShortcutHandlers) {
  const ref = useRef(handlers);
  ref.current = handlers;

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      const modifier = e.ctrlKey || e.metaKey;
      if (!modifier) return;
      if (e.altKey) return;

      // Ne jamais déclencher de raccourci global pendant la saisie dans un
      // champ (texte, zone, liste déroulante, contenu éditable) : les touches
      // restent dédiées à l'édition (Ctrl+S / Ctrl+F natives par exemple).
      if (isEditableTarget(e.target)) return;

      const key = e.key.toLowerCase();
      const h = ref.current;
      if (key === 'n' || key === 'j') {
        e.preventDefault();
        h.onNewInvoice();
      } else if (key === 's') {
        e.preventDefault();
        h.onSave();
      } else if (key === 'f') {
        e.preventDefault();
        h.onFocusSearch();
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);
}
