export interface UserPreferencesDto {
  hideValues: boolean;
}

export interface User {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
  permissions: string[];
  /** Preferências da conta (fonte de verdade é o servidor, não o localStorage). */
  preferences?: UserPreferencesDto | null;
}

export interface AuthResponse {
  accessToken: string;
  expiresIn: number;
  user: User;
}

export interface SignInDto {
  email: string;
  password: string;
}

export interface SignUpDto {
  email: string;
  password: string;
  fullName: string;
}
