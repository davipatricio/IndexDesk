'use client';

import {
  useQuery,
  useMutation,
  useQueryClient,
  type UseQueryOptions,
  type UseMutationOptions,
} from '@tanstack/react-query';
import {
  signIn,
  signUp,
  signOut,
  getCurrentUser,
  setAccessToken,
  type User,
  type AuthResponse,
  type SignInDto,
  type SignUpDto,
} from '@/lib/api-client';

export const authKeys = {
  all: ['auth'] as const,
  me: () => [...authKeys.all, 'me'] as const,
};

export function useCurrentUserQuery(
  options?: Partial<UseQueryOptions<User, Error, User, ReturnType<typeof authKeys.me>>>,
) {
  return useQuery({
    queryKey: authKeys.me(),
    queryFn: () => getCurrentUser(),
    staleTime: 5 * 60 * 1000,
    retry: false,
    ...options,
  });
}

export function useSignInMutation(
  options?: UseMutationOptions<AuthResponse, Error, SignInDto>,
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (dto: SignInDto) => signIn(dto),
    onSuccess: (data, variables, context, mutation) => {
      setAccessToken(data.accessToken);
      queryClient.setQueryData(authKeys.me(), data.user);
      options?.onSuccess?.(data, variables, context, mutation);
    },
    ...options,
  });
}

export function useSignUpMutation(
  options?: UseMutationOptions<AuthResponse, Error, SignUpDto>,
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (dto: SignUpDto) => signUp(dto),
    onSuccess: (data, variables, context, mutation) => {
      setAccessToken(data.accessToken);
      queryClient.setQueryData(authKeys.me(), data.user);
      options?.onSuccess?.(data, variables, context, mutation);
    },
    ...options,
  });
}

export function useSignOutMutation(
  options?: UseMutationOptions<void, Error, void>,
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => signOut(),
    onSuccess: (data, variables, context, mutation) => {
      setAccessToken(null);
      queryClient.setQueryData(authKeys.me(), null);
      queryClient.invalidateQueries({ queryKey: authKeys.all });
      options?.onSuccess?.(data, variables, context, mutation);
    },
    ...options,
  });
}
