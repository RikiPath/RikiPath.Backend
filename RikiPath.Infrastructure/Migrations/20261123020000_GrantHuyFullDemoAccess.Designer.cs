// This migration has no model changes; the designer is kept as a migration
// metadata pair so the repository's migration-governance checks can identify it.
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RikiPath.Infrastructure;

#nullable disable

namespace RikiPath.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261123020000_GrantHuyFullDemoAccess")]
partial class GrantHuyFullDemoAccess
{
}
