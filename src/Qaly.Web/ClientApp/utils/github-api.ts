import { apiCommand, apiResult } from './api-client'

export interface GitHubInstallation {
  id: string
  installationId: number
  accountLogin: string
  accountType: string
  status: string
  createdAt: string
}

export interface GitHubRepository {
  id: number
  owner: string
  name: string
  fullName: string
  defaultBranch: string
  isPrivate: boolean
}

export interface GitHubRepositoryConnection {
  id: string
  gitHubInstallationId: string
  repositoryExternalId: number
  fullName: string
  defaultBranch: string
  isPrivate: boolean
  isActive: boolean
  lastSyncedAt: string | null
}

export interface TaskDevelopment {
  taskId: string
  taskKey: string
  commits: Array<{ sha: string; message: string; authorLogin: string | null; committedAt: string; branchName: string | null; url: string; repository: string }>
  pullRequests: Array<{ number: number; title: string; state: string; isDraft: boolean; authorLogin: string | null; headBranch: string; baseBranch: string; reviewCount: number; approvalCount: number; mergedAt: string | null; url: string; repository: string }>
  workflowRuns: Array<{ runId: number; workflowName: string; displayTitle: string | null; branch: string; status: string; conclusion: string | null; startedAt: string; completedAt: string | null; url: string; repository: string }>
  releases: Array<{ tagName: string; name: string | null; publishedAt: string | null; url: string; repository: string }>
  links: Array<{ entityType: string; externalEntityId: string; linkSource: string; createdAt: string }>
}

export const githubApi = {
  installations: (projectId: string) => apiResult<GitHubInstallation[]>(`/api/projects/${projectId}/github/installations`),
  installUrl: (projectId: string) => apiResult<string>(`/api/projects/${projectId}/github/install-url`),
  completeInstallation: (projectId: string, installationId: number) =>
    apiResult<GitHubInstallation>(`/api/projects/${projectId}/github/installations/${installationId}/complete`, { method: 'POST' }),
  availableRepositories: (projectId: string, installationId: string) =>
    apiResult<GitHubRepository[]>(`/api/projects/${projectId}/github/installations/${installationId}/repositories`),
  connections: (projectId: string) => apiResult<GitHubRepositoryConnection[]>(`/api/projects/${projectId}/github/repositories`),
  connectRepository: (projectId: string, installationId: string, repository: GitHubRepository) =>
    apiResult<GitHubRepositoryConnection>(`/api/projects/${projectId}/github/repositories`, {
      method: 'POST',
      body: JSON.stringify({
        gitHubInstallationId: installationId,
        repositoryExternalId: repository.id,
        owner: repository.owner,
        name: repository.name,
      }),
    }),
  disconnectRepository: (projectId: string, connectionId: string) =>
    apiCommand(`/api/projects/${projectId}/github/repositories/${connectionId}`, { method: 'DELETE' }),
  development: (taskId: string) => apiResult<TaskDevelopment>(`/api/tasks/${taskId}/development`),
}
