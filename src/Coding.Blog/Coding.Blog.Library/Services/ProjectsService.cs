﻿using Coding.Blog.Library.Clients;
using Coding.Blog.Library.DataTransfer;
using Coding.Blog.Library.Protos;
using Coding.Blog.Library.Utilities;
using Grpc.Core;

namespace Coding.Blog.Library.Services;

public sealed class ProjectsService(ICosmicClient<CosmicProject> client, IMapper mapper) : Projects.ProjectsBase
{
    public override async Task<ProjectsReply> GetProjects(ProjectsRequest request, ServerCallContext context)
    {
        var cosmicProjects = await client.GetAsync().ConfigureAwait(false);
        var projects = mapper.Map<CosmicProject, Project>(cosmicProjects);

        return new ProjectsReply
        {
            Projects = { projects }
        };
    }
}
