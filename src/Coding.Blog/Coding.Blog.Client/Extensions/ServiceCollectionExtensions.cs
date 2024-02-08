﻿using Coding.Blog.Client.Clients;
using Coding.Blog.Client.Options;
using Coding.Blog.Client.Services;
using Coding.Blog.Client.Utilities;
using Coding.Blog.Library.Protos;
using Coding.Blog.Library.Services;
using Coding.Blog.Library.Utilities;
using ColorCode;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Grpc.Net.Client.Web;
using Markdig;
using Markdown.ColorCode;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Book = Coding.Blog.Library.Domain.Book;
using Post = Coding.Blog.Library.Domain.Post;
using Project = Coding.Blog.Library.Domain.Project;
using ProtoBook = Coding.Blog.Library.Protos.Book;
using ProtoPost = Coding.Blog.Library.Protos.Post;
using ProtoProject = Coding.Blog.Library.Protos.Project;

namespace Coding.Blog.Client.Extensions;

internal static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Configure the necessary services for the application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection
        ConfigureServices(this IServiceCollection services, IConfiguration configuration) => services
        .AddUtilities()
        .AddClients()
        .AddServices()
        .AddGrpc(configuration);

    private static IServiceCollection AddUtilities(this IServiceCollection services) =>
        services.AddSingleton(_ => new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseColorCode(
                    HtmlFormatterType.Style,
                    SyntaxHighlighting.Dark,
                    new List<ILanguage> { new CSharpOverride() })
                .Build())
            .AddSingleton<IMapper, Mapper>()
            .AddSingleton<IPostLinker, PostLinker>();

    private static IServiceCollection AddClients(this IServiceCollection services) =>
        services.AddSingleton<IProtoClient<ProtoPost>, PostsClient>()
            .AddSingleton<IProtoClient<ProtoBook>, BooksClient>()
            .AddSingleton<IProtoClient<ProtoProject>, ProjectsClient>();

    private static IServiceCollection AddServices(this IServiceCollection services) =>
        services.AddSingleton<IBlogService<Post>, BlogService<ProtoPost, Post>>()
            .AddSingleton<IBlogService<Book>, BlogService<ProtoBook, Book>>()
            .AddSingleton<IBlogService<Project>, BlogService<ProtoProject, Project>>()
            .AddSingleton<IPersistentComponentStateService<Post>, PersistentComponentStateService<Post>>()
            .AddSingleton<IPersistentComponentStateService<Book>, PersistentComponentStateService<Book>>()
            .AddSingleton<IPersistentComponentStateService<Project>, PersistentComponentStateService<Project>>()
            .AddSingleton<IJSInteropService, JSInteropService>();

    private static IServiceCollection AddGrpc(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GrpcOptions>()
            .Bind(configuration.GetSection(GrpcOptions.Key))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
            {
                var grpcOptions = serviceProvider.GetRequiredService<IOptions<GrpcOptions>>();

                return new ServiceConfig
                {
                    MethodConfigs =
                    {
                        new MethodConfig
                        {
                            Names = { MethodName.Default },
                            RetryPolicy = new RetryPolicy
                            {
                                MaxAttempts = grpcOptions.Value.MaxAttempts,
                                InitialBackoff = grpcOptions.Value.InitialBackoff,
                                MaxBackoff = grpcOptions.Value.MaxBackoff,
                                BackoffMultiplier = grpcOptions.Value.BackoffMultiplier,
                                RetryableStatusCodes = { StatusCode.Unavailable }
                            }
                        }
                    }
                };
            })
            .AddConfiguredGrpcClient<Posts.PostsClient>()
            .AddConfiguredGrpcClient<Books.BooksClient>()
            .AddConfiguredGrpcClient<Projects.ProjectsClient>();

        return services;
    }

    private static IServiceCollection AddConfiguredGrpcClient<T>(this IServiceCollection services) where T : ClientBase
    {
        services.AddGrpcClient<T>((serviceProvider, grpcClientFactoryOptions) =>
            {
                grpcClientFactoryOptions.Address =
                    new Uri(serviceProvider.GetRequiredService<NavigationManager>().BaseUri);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()))
            .ConfigureChannel((serviceProvider, grpcChannelOptions) =>
            {
                grpcChannelOptions.ServiceConfig = serviceProvider.GetRequiredService<ServiceConfig>();
            });

        return services;
    }
}
