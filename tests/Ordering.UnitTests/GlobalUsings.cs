global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using MediatR;
global using Microsoft.AspNetCore.Mvc;
global using eShop.Ordering.API.Application.Commands;
global using eShop.Ordering.API.Application.Models;
global using eShop.Ordering.API.Infrastructure.Services;
global using eShop.Ordering.Domain.AggregatesModel.BuyerAggregate;
global using eShop.Ordering.Domain.Events;
global using eShop.Ordering.Domain.Exceptions;
global using eShop.Ordering.Domain.SeedWork;
global using eShop.Ordering.Infrastructure.Idempotency;
global using eShop.Payment.Shared.Audit;
global using eShop.Payment.Shared.Idempotency;
global using eShop.Payment.Shared.Iso20022;
global using eShop.Payment.Shared.Security;
global using Microsoft.Extensions.Logging;
global using NSubstitute;
global using eShop.Ordering.UnitTests;
global using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
