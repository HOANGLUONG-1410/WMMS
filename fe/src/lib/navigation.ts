// src/libs/navigation.ts
type NavigateFn = (to: string) => void;

let navigateFn: NavigateFn | null = null;

export function setNavigate(fn: NavigateFn) {
  navigateFn = fn;
}

export function navigate(to: string) {
  if (navigateFn) {
    navigateFn(to);
  } else {
    window.location.href = to;
  }
}