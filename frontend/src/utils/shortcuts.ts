import { useEffect, useRef } from 'react';
import { useShortcutsStore } from '../stores/shortcutsStore';
import type { CommandId, ShortcutBinding } from '../stores/shortcutsStore';

export { COMMAND_IDS } from '../stores/shortcutsStore';
export type { CommandId, ShortcutBinding } from '../stores/shortcutsStore';

/** Correspondance clavier → commande. L'ordre détermine la priorité si deux
 * raccourcis matchent (le store garantit l'unicité). */
type Handler = () => void;

/** Handlers spécifiques à la page active (désenregistrés au démontage). */
const pageHandlers = new Map<CommandId, Handler>();
/** Handlers globaux (enregistrés une fois, ex. dans AppLayout ; actifs partout). */
const globalHandlers = new Map<CommandId, Handler>();

export function registerPageHandler(id: CommandId, handler: Handler): () => void {
  pageHandlers.set(id, handler);
  return () => {
    if (pageHandlers.get(id) === handler) pageHandlers.delete(id);
  };
}

export function registerGlobalHandler(id: CommandId, handler: Handler): () => void {
  globalHandlers.set(id, handler);
  return () => {
    if (globalHandlers.get(id) === handler) globalHandlers.delete(id);
  };
}

/** Déclenche le handler réellement enregistré (page en priorité, sinon global). */
export function invokeCommand(id: CommandId): void {
  const h = pageHandlers.get(id) ?? globalHandlers.get(id);
  h?.();
}

/**
 * Enregistre un handler SPÉCIFIQUE À LA PAGE. Le raccourci n'est actif que
 * tant que la page est montée ; permet un comportement contextuel (ex. Ctrl+S
 * enregistre la facture sur la page facture, les réglages sur Options…).
 * Le handler est relu à chaque rendu via une ref (aucune version périmée).
 */
export function useCommand(id: CommandId, handler: () => void) {
  const ref = useRef(handler);
  ref.current = handler;

  useEffect(() => {
    const fn = () => ref.current();
    pageHandlers.set(id, fn);
    return () => {
      if (pageHandlers.get(id) === fn) pageHandlers.delete(id);
    };
  }, [id]);
}

/**
 * Enregistre un handler GLOBAL, actif sur toutes les pages tant que le
 * composant (ex. AppLayout) est monté. Les handlers page priment sur les
 * handlers globaux pour une même commande.
 */
export function useGlobalCommand(id: CommandId, handler: () => void) {
  const ref = useRef(handler);
  ref.current = handler;

  useEffect(() => {
    const fn = () => ref.current();
    globalHandlers.set(id, fn);
    return () => {
      if (globalHandlers.get(id) === fn) globalHandlers.delete(id);
    };
  }, [id]);
}

/** Flag positionné pendant l'ouverture du Command Palette pour suspendre les
 * raccourcis globaux (la palette gère elle-même ses touches). */
let paletteOpen = false;
export function setShortcutPaletteOpen(value: boolean): void {
  paletteOpen = value;
}

/** Cibles ignorées pendant la saisie dans un champ texte/zone/liste. */
function isEditableTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target.isContentEditable;
}

function matches(e: KeyboardEvent, b: ShortcutBinding): boolean {
  return (
    e.code === b.code &&
    e.ctrlKey === b.ctrl &&
    e.altKey === b.alt &&
    e.shiftKey === b.shift &&
    e.metaKey === b.meta
  );
}

/**
 * Écoute globale des raccourcis configurés par l'utilisateur. La correspondance
 * se fait sur e.code (touche physique) : indépendante de la disposition de
 * clavier (QWERTY, AZERTY, arabe…).
 *
 * Règle d'interception :
 *  - Les combinaisons avec modificateur (Ctrl/Alt/Meta), les touches de fonction
 *    (F1…F12) et Échap sont TOUJOURS interceptées, même en saisie, car ce sont
 *    des commandes applicatives (et Échap sert à annuler/fermer). Cela empêche
 *    notamment Ctrl+S / Ctrl+P de déclencher le comportement du navigateur.
 *  - Les touches simples sans modificateur (Entrée, etc.) sont ignorées pendant
 *    la saisie pour ne pas perturber la frappe.
 */
export function useGlobalShortcuts() {
  const bindings = useShortcutsStore((s) => s.bindings);

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (paletteOpen) return;

      if (isEditableTarget(e.target)) {
        const hasModifier = e.ctrlKey || e.altKey || e.metaKey;
        const isFnKey = /^F\d{1,2}$/.test(e.key);
        if (!hasModifier && !isFnKey && e.key !== 'Escape') return;
      }

      for (const id of Object.keys(bindings) as CommandId[]) {
        const b = bindings[id];
        if (b && matches(e, b)) {
          e.preventDefault();
          e.stopPropagation();
          invokeCommand(id);
          return;
        }
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [bindings]);
}
