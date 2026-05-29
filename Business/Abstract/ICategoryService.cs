using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Utilities.Results;
using Entities.Concrete.Dto;
using Entities.Concrete.Entities;

namespace Business.Abstract
{
    public interface ICategoryService
    {
        Task<IDataResult<List<Category>>> GetAllCategories();
        Task<IDataResult<List<Category>>> GetParentCategories();
        Task<IDataResult<List<Category>>> GetChildCategories(Guid parentId);
        Task<IDataResult<List<CategoryHierarchyDto>>> GetCategoryHierarchyAsync();
        Task<IResult> AddCategory(Category category);
        Task<IResult> DeleteCategory(Guid id);

        /// <summary>
        /// Kategori adını ve/veya parent'ını günceller. Cycle koruması yapar:
        /// bir kategori kendi descendant'ı altına taşınamaz.
        /// </summary>
        Task<IResult> UpdateCategory(Guid id, string name, Guid? parentId);

        /// <summary>
        /// Kategoriyi siler ve doğrudan child'larını verilen yeni parent'a taşır (null = root).
        /// </summary>
        Task<IResult> DeleteCategoryAndReparent(Guid id, Guid? reparentTo);
    }
}
