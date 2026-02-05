import { useQuery } from '@tanstack/react-query';
import type { Api } from '@jellyfin/sdk/lib/api';
import { getSyncPlayApi } from '@jellyfin/sdk/lib/utils/api/sync-play-api';
import type { AxiosRequestConfig } from 'axios';

import { useApi } from './useApi';

const fetchSyncPlayGroups = async (
    api: Api,
    options?: AxiosRequestConfig
) => {
    const response = await getSyncPlayApi(api)
        .syncPlayGetGroups(options);
    return response.data;
};

interface UseSyncPlayGroupsOptions {
    enabled?: boolean
    refetchInterval?: number | false
}

export const useSyncPlayGroups = (options: UseSyncPlayGroupsOptions = {}) => {
    const { api } = useApi();
    const {
        enabled = true,
        refetchInterval = false
    } = options;

    return useQuery({
        queryKey: [ 'SyncPlay', 'Groups' ],
        queryFn: ({ signal }) => fetchSyncPlayGroups(api!, { signal }),
        enabled: !!api && enabled,
        refetchInterval
    });
};
