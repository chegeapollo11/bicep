// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO.Abstractions;
using Bicep.Cli.Arguments;
using Bicep.Core.Diagnostics;
using Bicep.Core.Exceptions;
using Bicep.Core.Features;
using Bicep.Core.FileSystem;
using Bicep.Core.Modules;
using Bicep.Core.Registry;
using Bicep.Core.Registry.Providers;
using Bicep.Core.TypeSystem;
using Microsoft.Extensions.Logging;

namespace Bicep.Cli.Commands
{
    public class PublishProviderCommand : ICommand
    {
        private readonly IModuleDispatcher moduleDispatcher;
        private readonly IFileSystem fileSystem;
        private readonly IFeatureProviderFactory featureProviderFactory;
        private readonly IOContext ioContext;
        private readonly ILogger logger;

        public PublishProviderCommand(
            IOContext ioContext,
            ILogger logger,
            IModuleDispatcher moduleDispatcher,
            IFileSystem fileSystem,
            IFeatureProviderFactory featureProviderFactory)
        {
            this.moduleDispatcher = moduleDispatcher;
            this.fileSystem = fileSystem;
            this.featureProviderFactory = featureProviderFactory;
            this.ioContext = ioContext;
            this.logger = logger;
        }

        public async Task<int> RunAsync(PublishProviderArguments args)
        {
            await ioContext.Error.WriteLineAsync("The 'publish-provider' CLI command group is an experimental feature. Experimental features should be enabled for testing purposes only, as there are no guarantees about the quality or stability of these features. Do not enable these settings for any production usage, or your production environment may be subject to breaking.");

            var indexPath = PathHelper.ResolvePath(args.IndexFile);
            var indexUri = PathHelper.FilePathToFileUrl(indexPath);
            var providerReference = ValidateReference(args.TargetProviderReference, indexUri);
            var overwriteIfExists = args.Force;

            BinaryData tarPayload;
            try
            {
                tarPayload = await TypesV1Archive.GenerateProviderTarStream(this.fileSystem, indexPath);
                ValidateProvider(tarPayload);
            }
            catch (Exception exception)
            {
                throw new BicepException($"Provider package creation failed: {exception.Message}");
            }

            await this.PublishProviderAsync(providerReference, tarPayload, overwriteIfExists);
            return 0;
        }

        private async Task PublishProviderAsync(ArtifactReference target, BinaryData tarPayload, bool overwriteIfExists)
        {
            try
            {
                // If we don't want to overwrite, ensure provider doesn't exist
                if (!overwriteIfExists && await this.moduleDispatcher.CheckProviderExists(target))
                {
                    throw new BicepException($"The Provider \"{target.FullyQualifiedReference}\" already exists. Use --force to overwrite the existing provider.");
                }
                await this.moduleDispatcher.PublishProvider(target, tarPayload);
            }
            catch (ExternalArtifactException exception)
            {
                throw new BicepException($"Unable to publish provider \"{target.FullyQualifiedReference}\": {exception.Message}");
            }
        }

        private ArtifactReference ValidateReference(string targetProviderReference, Uri targetProviderUri)
        {
            if (!targetProviderReference.StartsWith("br:"))
            {
                // convert to a relative path, as this is the only format supported for the local filesystem
                targetProviderUri = PathHelper.FilePathToFileUrl(PathHelper.ResolvePath(targetProviderReference));
                targetProviderReference = Path.GetFileName(targetProviderUri.LocalPath);
            }

            if (!this.moduleDispatcher.TryGetArtifactReference(ArtifactType.Provider, targetProviderReference, targetProviderUri).IsSuccess(out var providerReference, out var failureBuilder))
            {
                // TODO: We should probably clean up the dispatcher contract so this sort of thing isn't necessary (unless we change how target module is set in this command)
                var message = failureBuilder(DiagnosticBuilder.ForDocumentStart()).Message;

                throw new BicepException(message);
            }

            if (!this.moduleDispatcher.GetRegistryCapabilities(ArtifactType.Provider, providerReference).HasFlag(RegistryCapabilities.Publish))
            {
                throw new BicepException($"The specified provider target \"{targetProviderReference}\" is not supported.");
            }

            return providerReference;
        }

        private static void ValidateProvider(BinaryData provider)
        {
            using var tempStream = provider.ToStream();

            var typeLoader = OciTypeLoader.FromStream(tempStream);
            var index = typeLoader.LoadTypeIndex();
            foreach (var (_, typeLocation) in index.Resources)
            {
                typeLoader.LoadResourceType(typeLocation);
            }
        }
    }
}
