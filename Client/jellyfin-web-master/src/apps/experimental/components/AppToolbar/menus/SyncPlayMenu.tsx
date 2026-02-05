import type { GroupInfoDto } from '@jellyfin/sdk/lib/generated-client/models/group-info-dto';
import { SyncPlayUserAccessType } from '@jellyfin/sdk/lib/generated-client/models/sync-play-user-access-type';
import { getSyncPlayApi } from '@jellyfin/sdk/lib/utils/api/sync-play-api';
import GroupAdd from '@mui/icons-material/GroupAdd';
import PersonAdd from '@mui/icons-material/PersonAdd';
import PersonOff from '@mui/icons-material/PersonOff';
import PersonRemove from '@mui/icons-material/PersonRemove';
import PlayCircle from '@mui/icons-material/PlayCircle';
import StopCircle from '@mui/icons-material/StopCircle';
import Tune from '@mui/icons-material/Tune';
import Divider from '@mui/material/Divider';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import ListSubheader from '@mui/material/ListSubheader';
import Menu, { MenuProps } from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import type { ApiClient } from 'jellyfin-apiclient';
import React, { FC, useCallback, useEffect, useState } from 'react';

import { pluginManager } from 'components/pluginManager';
import { useApi } from 'hooks/useApi';
import { useSyncPlayGroups } from 'hooks/useSyncPlayGroups';
import globalize from 'lib/globalize';
import { PluginType } from 'types/plugin';
import { queryClient } from 'utils/query/queryClient';
import Events from 'utils/events';

export const ID = 'app-sync-play-menu';

interface SyncPlayMenuProps extends MenuProps {
    onMenuClose: () => void
}

interface SyncPlayInstance {
    Manager: {
        getGroupInfo: () => GroupInfoDto | null | undefined
        getTimeSyncCore: () => object
        isPlaybackActive: () => boolean
        isPlaylistEmpty: () => boolean
        haltGroupPlayback: (apiClient: ApiClient) => void
        resumeGroupPlayback: (apiClient: ApiClient) => void
        enableSyncPlay: (apiClient: ApiClient, groupInfo: GroupInfoDto, showMessage: boolean) => void
    }
}

/* eslint-disable sonarjs/cognitive-complexity */
const SyncPlayMenu: FC<SyncPlayMenuProps> = ({
    anchorEl,
    open,
    onMenuClose
}) => {
    const [ syncPlay, setSyncPlay ] = useState<SyncPlayInstance>();
    const { __legacyApiClient__, api, user } = useApi();
    const [ currentGroup, setCurrentGroup ] = useState<GroupInfoDto>();
    const isSyncPlayEnabled = Boolean(currentGroup);

    useEffect(() => {
        setSyncPlay(pluginManager.firstOfType(PluginType.SyncPlay)?.instance);
    }, []);

    const updateSyncPlayGroup = useCallback(() => {
        const group = syncPlay?.Manager.getGroupInfo() ?? undefined;
        setCurrentGroup(group);
    }, [ syncPlay ]);

    const enableSyncPlayGroup = useCallback((groupInfo?: GroupInfoDto) => {
        if (!groupInfo?.GroupId || !syncPlay || !__legacyApiClient__) {
            return false;
        }

        const normalizedGroupInfo = { ...groupInfo } as Record<string, unknown>;
        normalizedGroupInfo.LastUpdatedAt = groupInfo.LastUpdatedAt ?
            new Date(groupInfo.LastUpdatedAt) :
            new Date();
        syncPlay.Manager.enableSyncPlay(__legacyApiClient__, normalizedGroupInfo as GroupInfoDto, false);
        setCurrentGroup(normalizedGroupInfo as GroupInfoDto);
        return true;
    }, [ __legacyApiClient__, syncPlay ]);

    const fetchAndEnableSyncPlayGroup = useCallback(async (groupId?: string) => {
        if (!groupId || !__legacyApiClient__) {
            return false;
        }

        try {
            const url = __legacyApiClient__.getUrl(`SyncPlay/${groupId}`, { _: Date.now() });
            const groupInfo = await __legacyApiClient__.getJSON(url) as GroupInfoDto | undefined;
            return enableSyncPlayGroup(groupInfo);
        } catch (err) {
            console.error('[SyncPlayMenu] failed to fetch SyncPlay group details', err);
            return false;
        }
    }, [ __legacyApiClient__, enableSyncPlayGroup ]);

    const { data: groups } = useSyncPlayGroups({
        enabled: open && !isSyncPlayEnabled,
        refetchInterval: open && !isSyncPlayEnabled ? 2000 : false
    });

    const onGroupAddClick = useCallback(async () => {
        if (api && user) {
            try {
                const response = await getSyncPlayApi(api).syncPlayCreateGroup({
                    newGroupRequestDto: {
                        GroupName: globalize.translate('SyncPlayGroupDefaultTitle', user.Name)
                    }
                });
                const groupInfo = response?.data;
                if (groupInfo?.GroupId) {
                    enableSyncPlayGroup(groupInfo);
                }
            } catch (err) {
                console.error('[SyncPlayMenu] failed to create a SyncPlay group', err);
            } finally {
                void queryClient.invalidateQueries({
                    queryKey: [ 'SyncPlay', 'Groups' ]
                });
            }

            onMenuClose();
        }
    }, [ api, enableSyncPlayGroup, onMenuClose, user ]);

    const onGroupLeaveClick = useCallback(() => {
        if (api) {
            getSyncPlayApi(api)
                .syncPlayLeaveGroup()
                .catch(err => {
                    console.error('[SyncPlayMenu] failed to leave SyncPlay group', err);
                });

            onMenuClose();
        }
    }, [ api, onMenuClose ]);

    const onGroupJoinClick = useCallback(async (GroupId: string) => {
        if (api) {
            try {
                await getSyncPlayApi(api).syncPlayJoinGroup({
                    joinGroupRequestDto: {
                        GroupId
                    }
                });
                await fetchAndEnableSyncPlayGroup(GroupId);
            } catch (err) {
                console.error('[SyncPlayMenu] failed to join SyncPlay group', err);
            } finally {
                void queryClient.invalidateQueries({
                    queryKey: [ 'SyncPlay', 'Groups' ]
                });
            }

            onMenuClose();
        }
    }, [ api, fetchAndEnableSyncPlayGroup, onMenuClose ]);

    const onGroupSettingsClick = useCallback(async () => {
        if (!syncPlay) return;

        // TODO: Rewrite settings UI
        const SyncPlaySettingsEditor = (await import('../../../../../plugins/syncPlay/ui/settings/SettingsEditor')).default;
        new SyncPlaySettingsEditor(
            __legacyApiClient__,
            syncPlay.Manager.getTimeSyncCore(),
            {
                groupInfo: currentGroup
            })
            .embed()
            .catch(err => {
                if (err) {
                    console.error('[SyncPlayMenu] Error creating SyncPlay settings editor', err);
                }
            });

        onMenuClose();
    }, [ __legacyApiClient__, currentGroup, onMenuClose, syncPlay ]);

    const onStartGroupPlaybackClick = useCallback(() => {
        if (__legacyApiClient__) {
            syncPlay?.Manager.resumeGroupPlayback(__legacyApiClient__);
            onMenuClose();
        }
    }, [ __legacyApiClient__, onMenuClose, syncPlay ]);

    const onStopGroupPlaybackClick = useCallback(() => {
        if (__legacyApiClient__) {
            syncPlay?.Manager.haltGroupPlayback(__legacyApiClient__);
            onMenuClose();
        }
    }, [ __legacyApiClient__, onMenuClose, syncPlay ]);

    useEffect(() => {
        if (!syncPlay) return;

        updateSyncPlayGroup();
        Events.on(syncPlay.Manager, 'enabled', updateSyncPlayGroup);
        Events.on(syncPlay.Manager, 'group-state-update', updateSyncPlayGroup);
        Events.on(syncPlay.Manager, 'playerchange', updateSyncPlayGroup);

        return () => {
            Events.off(syncPlay.Manager, 'enabled', updateSyncPlayGroup);
            Events.off(syncPlay.Manager, 'group-state-update', updateSyncPlayGroup);
            Events.off(syncPlay.Manager, 'playerchange', updateSyncPlayGroup);
        };
    }, [ updateSyncPlayGroup, syncPlay ]);

    useEffect(() => {
        if (open) {
            updateSyncPlayGroup();
        }
    }, [ open, updateSyncPlayGroup ]);

    const menuItems = [];
    if (isSyncPlayEnabled) {
        if (!syncPlay?.Manager.isPlaylistEmpty() && !syncPlay?.Manager.isPlaybackActive()) {
            menuItems.push(
                <MenuItem
                    key='sync-play-start-playback'
                    onClick={onStartGroupPlaybackClick}
                >
                    <ListItemIcon>
                        <PlayCircle />
                    </ListItemIcon>
                    <ListItemText primary={globalize.translate('LabelSyncPlayResumePlayback')} />
                </MenuItem>
            );
        } else if (syncPlay?.Manager.isPlaybackActive()) {
            menuItems.push(
                <MenuItem
                    key='sync-play-stop-playback'
                    onClick={onStopGroupPlaybackClick}
                >
                    <ListItemIcon>
                        <StopCircle />
                    </ListItemIcon>
                    <ListItemText primary={globalize.translate('LabelSyncPlayHaltPlayback')} />
                </MenuItem>
            );
        }

        menuItems.push(
            <MenuItem
                key='sync-play-settings'
                onClick={onGroupSettingsClick}
            >
                <ListItemIcon>
                    <Tune />
                </ListItemIcon>
                <ListItemText
                    primary={globalize.translate('Settings')}
                />
            </MenuItem>
        );

        menuItems.push(
            <Divider key='sync-play-controls-divider' />
        );

        menuItems.push(
            <MenuItem
                key='sync-play-exit'
                onClick={onGroupLeaveClick}
            >
                <ListItemIcon>
                    <PersonRemove />
                </ListItemIcon>
                <ListItemText
                    primary={globalize.translate('LabelSyncPlayLeaveGroup')}
                />
            </MenuItem>
        );
    } else if (!groups?.length && user?.Policy?.SyncPlayAccess !== SyncPlayUserAccessType.CreateAndJoinGroups) {
        menuItems.push(
            <MenuItem key='sync-play-unavailable' disabled>
                <ListItemIcon>
                    <PersonOff />
                </ListItemIcon>
                <ListItemText primary={globalize.translate('LabelSyncPlayNoGroups')} />
            </MenuItem>
        );
    } else {
        if (groups && groups.length > 0) {
            groups.forEach(group => {
                menuItems.push(
                    <MenuItem
                        key={group.GroupId}
                        // Since we are looping over groups there is no good way to avoid creating a new function here
                        // eslint-disable-next-line react/jsx-no-bind
                        onClick={() => group.GroupId && onGroupJoinClick(group.GroupId)}
                    >
                        <ListItemIcon>
                            <PersonAdd />
                        </ListItemIcon>
                        <ListItemText
                            primary={group.GroupName}
                            secondary={group.Participants?.join(', ')}
                        />
                    </MenuItem>
                );
            });

            menuItems.push(
                <Divider key='sync-play-groups-divider' />
            );
        }

        if (user?.Policy?.SyncPlayAccess === SyncPlayUserAccessType.CreateAndJoinGroups) {
            menuItems.push(
                <MenuItem
                    key='sync-play-new-group'
                    onClick={onGroupAddClick}
                >
                    <ListItemIcon>
                        <GroupAdd />
                    </ListItemIcon>
                    <ListItemText primary={globalize.translate('LabelSyncPlayNewGroupDescription')} />
                </MenuItem>
            );
        }
    }

    const MenuListProps = isSyncPlayEnabled ? {
        'aria-labelledby': 'sync-play-active-subheader',
        subheader: (
            <ListSubheader component='div' id='sync-play-active-subheader'>
                {currentGroup?.GroupName}
            </ListSubheader>
        )
    } : undefined;

    return (
        <Menu
            anchorEl={anchorEl}
            anchorOrigin={{
                vertical: 'bottom',
                horizontal: 'right'
            }}
            transformOrigin={{
                vertical: 'top',
                horizontal: 'right'
            }}
            id={ID}
            keepMounted
            open={open}
            onClose={onMenuClose}
            slotProps={{
                list: MenuListProps
            }}
        >
            {menuItems}
        </Menu>
    );
};
/* eslint-enable sonarjs/cognitive-complexity */

export default SyncPlayMenu;
