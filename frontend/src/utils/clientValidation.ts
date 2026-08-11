import type { ClientType, PaymentMethod } from '../api/types';
import { detectMobileOperator } from './companyValidation';
import { findCommune, getWilaya, isValidPostalCode } from '../data/algeriaLocations';
import type { ValidationErrors } from './companyValidation';

export interface ClientForm {
  displayName: string;
  companyName: string;
  sector: string;
  nif: string;
  rc: string;
  art: string;
  address: string;
  postalCode: string;
  city: string;
  wilaya: string;
  phone: string;
  mobile: string;
  email: string;
  type: ClientType;
  defaultPaymentMethod: PaymentMethod;
  notes: string;
}

export type FieldMark = 'ok' | 'warn' | 'error' | 'none';

const PHONE_REGEX = /^(?:0[567]\d{8}|\d{9})$/;
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.(com|dz|net|org)$/i;
const RC_REGEX = /^\d{2}\/\d{2}-\d{7}[A-Z]\d{2}$/;

export function validateClientDisplayName(value: string): string {
  const v = value.trim();
  if (!v) return 'Le nom du client est obligatoire.';
  if (v.length < 2) return 'Le nom du client doit contenir au moins 2 caractères.';
  return '';
}

export function validateClientNIF(value: string, type: ClientType): string {
  const v = value.trim();
  if (!v) return type === 'Entreprise' ? 'Le NIF est obligatoire pour une entreprise.' : '';
  if (!/^\d+$/.test(v)) return 'Le NIF ne doit contenir que des chiffres.';
  if (v.length !== 15) return `Le NIF doit contenir exactement 15 chiffres (${v.length}/15).`;
  return '';
}

export function validateClientRC(value: string): string {
  const v = value.trim();
  if (!v) return '';
  if (!RC_REGEX.test(v)) return 'Format RC attendu : 16/00-0000000B00.';
  return '';
}

export function validateClientART(value: string): string {
  const v = value.trim();
  if (!v) return '';
  if (!/^\d+$/.test(v)) return 'Le ART ne doit contenir que des chiffres.';
  if (v.length !== 13) return `Le ART doit contenir exactement 13 chiffres (${v.length}/13).`;
  return '';
}

export function validateClientPhone(value: string, required = true): string {
  const v = value.trim().replace(/\s/g, '');
  if (!v) return required ? 'Le téléphone est obligatoire.' : '';
  if (!/^\d+$/.test(v)) return 'Le téléphone ne doit contenir que des chiffres.';
  if (!PHONE_REGEX.test(v)) return 'Le téléphone doit être 05/06/07 + 8 chiffres, ou 9 chiffres.';
  return '';
}

export function validateClientEmail(value: string): string {
  const v = value.trim();
  if (!v) return '';
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v)) return "Format d'e-mail invalide.";
  if (!EMAIL_REGEX.test(v)) return 'Le domaine doit se terminer par .com, .dz, .net ou .org.';
  return '';
}

export function validateClientAddress(value: string): string {
  const v = value.trim();
  if (!v) return "L'adresse est obligatoire.";
  if (v.length < 3) return "L'adresse doit contenir au moins 3 caractères.";
  return '';
}

export function validateClientWilaya(value: string): string {
  const v = value.trim();
  if (!v) return '';
  if (!getWilaya(v)) return 'Wilaya invalide (découpage 2026 à 69 wilayas).';
  return '';
}

export function validateClientCity(wilaya: string, city: string): string {
  const c = city.trim();
  if (!c) return '';
  if (!wilaya) return 'La ville requiert une wilaya.';
  if (!findCommune(wilaya, c)) return 'La ville doit être une commune de la wilaya sélectionnée.';
  return '';
}

export function validateClientPostalCode(wilaya: string, city: string, postalCode: string): string {
  const code = postalCode.trim();
  if (!code) return '';
  if (!/^\d{5}$/.test(code)) return 'Le code postal doit contenir exactement 5 chiffres.';
  if (!isValidPostalCode(wilaya, city, code)) {
    const wilayaInfo = getWilaya(wilaya);
    if (city && wilayaInfo) {
      return `Ce code n'appartient pas aux codes officiels de la commune (${wilayaInfo.code} ${wilayaInfo.nameFr}).`;
    }
    return 'Code postal invalide pour la wilaya sélectionnée.';
  }
  return '';
}

export function validateClientForm(form: ClientForm): ValidationErrors {
  const errors: ValidationErrors = {};

  const displayName = validateClientDisplayName(form.displayName);
  if (displayName) errors.displayName = displayName;

  const nif = validateClientNIF(form.nif, form.type);
  if (nif) errors.nif = nif;

  const rc = validateClientRC(form.rc);
  if (rc) errors.rc = rc;

  const art = validateClientART(form.art);
  if (art) errors.art = art;

  const phone = validateClientPhone(form.phone);
  if (phone) errors.phone = phone;

  const mobile = validateClientPhone(form.mobile, false);
  if (mobile) errors.mobile = mobile;

  const email = validateClientEmail(form.email);
  if (email) errors.email = email;

  const address = validateClientAddress(form.address);
  if (address) errors.address = address;

  const wilaya = validateClientWilaya(form.wilaya);
  if (wilaya) errors.wilaya = wilaya;

  const city = validateClientCity(form.wilaya, form.city);
  if (city) errors.city = city;

  const postalCode = validateClientPostalCode(form.wilaya, form.city, form.postalCode);
  if (postalCode) errors.postalCode = postalCode;

  return errors;
}

/**
 * État d'un champ pour l'affichage ✓ / ⚠️ / ❌ :
 * - error : valeur renseignée et invalide ;
 * - ok : valeur renseignée et valide ;
 * - warn : champ obligatoire vide ;
 * - none : champ optionnel vide.
 */
export function markFor(fieldValue: string, error: string | undefined, required: boolean): FieldMark {
  if (error) return 'error';
  if (fieldValue.trim()) return 'ok';
  return required ? 'warn' : 'none';
}

export function operatorOf(phone: string): string {
  return detectMobileOperator(phone);
}
